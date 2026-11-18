import * as net from "net";
import { RevitBridgeError, errorFromRpc } from "./RevitErrors.js";

/**
 * Hoe lang de client maximaal op een antwoord wacht. Ruimer dan de langste command-timeout in
 * de commandset (CommandTimeouts.Heavy = 120 s), zodat een time-out normaal uit Revit komt
 * (met de garantie "niet uitgevoerd") en niet hier.
 */
export const CLIENT_TIMEOUT_MS = 150_000;

interface PendingRequest {
  method: string;
  resolve: (result: any) => void;
  reject: (error: Error) => void;
  timer: NodeJS.Timeout;
}

/**
 * TCP-verbinding met de Revit-plugin (JSON-RPC 2.0).
 *
 * Framing: elk bericht is één regel JSON, afgesloten met "\n" (JSON.stringify en de compacte
 * serialisatie van de plugin bevatten zelf geen kale newlines). Overgangsvorm: een oudere plugin
 * stuurt antwoorden zonder "\n"; zolang er geen "\n" in de buffer staat, wordt daarom ook
 * geprobeerd de hele buffer als één bericht te parsen.
 */
export class RevitClientConnection {
  host: string;
  port: number;
  socket: net.Socket;
  isConnected: boolean = false;
  private pending: Map<string, PendingRequest> = new Map();
  private buffer: string = "";
  // Positie tot waar de buffer al op "\n" is doorzocht, zodat elk stuk maar één keer gescand wordt
  private scanFrom: number = 0;

  constructor(host: string, port: number) {
    this.host = host;
    this.port = port;
    this.socket = new net.Socket();
    // Laat Node de UTF-8 decoderen: een teken van meerdere bytes dat over twee pakketten
    // verdeeld binnenkomt, wordt dan correct samengevoegd.
    this.socket.setEncoding("utf8");
    this.setupSocketListeners();
  }

  private setupSocketListeners(): void {
    this.socket.on("connect", () => {
      this.isConnected = true;
    });

    this.socket.on("data", (chunk: string) => {
      this.buffer += chunk;
      this.processBuffer();
    });

    this.socket.on("close", () => {
      this.isConnected = false;
      this.rejectAllPending(
        new RevitBridgeError("no_response", "Connection to Revit closed before a response was received")
      );
    });

    this.socket.on("error", (error) => {
      console.error("RevitClientConnection error:", error);
      this.isConnected = false;
    });
  }

  private processBuffer(): void {
    // Nieuw formaat: alle volledige regels afhandelen
    let newlineIndex = this.buffer.indexOf("\n", this.scanFrom);
    while (newlineIndex !== -1) {
      const line = this.buffer.slice(0, newlineIndex).trim();
      this.buffer = this.buffer.slice(newlineIndex + 1);
      this.scanFrom = 0;
      if (line.length > 0) {
        this.handleMessage(line);
      }
      newlineIndex = this.buffer.indexOf("\n");
    }
    this.scanFrom = this.buffer.length;

    // Overgangsvorm voor een oudere plugin zonder "\n": probeer de hele buffer, maar alleen als
    // die eruitziet als een afgerond JSON-object (voorkomt een parse-poging per binnenkomend stuk).
    const trimmed = this.buffer.trimEnd();
    if (trimmed.endsWith("}")) {
      try {
        JSON.parse(trimmed);
      } catch {
        return; // nog niet compleet, wachten op meer data
      }
      this.buffer = "";
      this.scanFrom = 0;
      this.handleMessage(trimmed);
    }
  }

  private handleMessage(message: string): void {
    let response: any;
    try {
      response = JSON.parse(message);
    } catch (error) {
      console.error("Error parsing response from Revit:", error);
      return;
    }

    const requestId = response?.id ?? "default";
    const request = this.pending.get(requestId);
    if (!request) {
      return; // antwoord op een verzoek dat al een time-out kreeg
    }

    this.pending.delete(requestId);
    clearTimeout(request.timer);

    if (response.error) {
      request.reject(errorFromRpc(response.error));
    } else {
      request.resolve(response.result);
    }
  }

  private rejectAllPending(error: Error): void {
    for (const request of this.pending.values()) {
      clearTimeout(request.timer);
      request.reject(error);
    }
    this.pending.clear();
  }

  public connect(): boolean {
    if (this.isConnected) {
      return true;
    }

    try {
      this.socket.connect(this.port, this.host);
      return true;
    } catch (error) {
      console.error("Failed to connect:", error);
      return false;
    }
  }

  public disconnect(): void {
    this.rejectAllPending(
      new RevitBridgeError("no_response", "Connection to Revit was closed by the client")
    );
    this.socket.destroy();
    this.isConnected = false;
  }

  private generateRequestId(): string {
    return Date.now().toString() + Math.random().toString().substring(2, 8);
  }

  public sendCommand(command: string, params: any = {}): Promise<any> {
    return new Promise((resolve, reject) => {
      try {
        if (!this.isConnected) {
          this.connect();
        }

        const requestId = this.generateRequestId();

        // Verzoekobject volgens de JSON-RPC-standaard
        const commandObj = {
          jsonrpc: "2.0",
          method: command,
          params: params,
          id: requestId,
        };

        const timer = setTimeout(() => {
          if (this.pending.delete(requestId)) {
            reject(
              new RevitBridgeError(
                "no_response",
                `No response from Revit within ${CLIENT_TIMEOUT_MS / 1000} s: ${command}`
              )
            );
          }
        }, CLIENT_TIMEOUT_MS);

        this.pending.set(requestId, { method: command, resolve, reject, timer });

        // Eén regel JSON, afgesloten met "\n"
        this.socket.write(JSON.stringify(commandObj) + "\n");
      } catch (error) {
        reject(error);
      }
    });
  }
}
