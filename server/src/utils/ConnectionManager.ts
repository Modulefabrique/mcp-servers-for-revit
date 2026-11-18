import { RevitClientConnection } from "./SocketClient.js";
import { RevitBridgeError } from "./RevitErrors.js";

// Mutex om alle Revit-verbindingen te serialiseren: voorkomt race conditions
// wanneer meerdere verzoeken tegelijk binnenkomen
let connectionMutex: Promise<void> = Promise.resolve();

const CONNECT_TIMEOUT_MS = 5000;

/**
 * Maakt verbinding met de Revit-client en voert een operatie uit
 * @param operation De functie die moet worden uitgevoerd na een succesvolle verbinding
 * @returns Het resultaat van de operatie
 */
export async function withRevitConnection<T>(
  operation: (client: RevitClientConnection) => Promise<T>
): Promise<T> {
  // Wacht tot een lopend verzoek klaar is voordat een nieuw verzoek start
  const previousMutex = connectionMutex;
  let releaseMutex: () => void;
  connectionMutex = new Promise<void>((resolve) => {
    releaseMutex = resolve;
  });
  await previousMutex;

  const revitClient = new RevitClientConnection("localhost", 8080);

  try {
    // Verbinding maken met de Revit-client
    if (!revitClient.isConnected) {
      await new Promise<void>((resolve, reject) => {
        let timer: NodeJS.Timeout;

        const cleanup = () => {
          clearTimeout(timer);
          revitClient.socket.removeListener("connect", onConnect);
          revitClient.socket.removeListener("error", onError);
        };

        const onConnect = () => {
          cleanup();
          resolve();
        };

        const onError = (error: any) => {
          cleanup();
          reject(
            new RevitBridgeError(
              "connection_failed",
              `Connection to Revit failed: ${error?.message ?? String(error)}`
            )
          );
        };

        revitClient.socket.on("connect", onConnect);
        revitClient.socket.on("error", onError);

        revitClient.connect();

        timer = setTimeout(() => {
          cleanup();
          reject(
            new RevitBridgeError(
              "connection_failed",
              `Connection to Revit timed out after ${CONNECT_TIMEOUT_MS / 1000} s`
            )
          );
        }, CONNECT_TIMEOUT_MS);
      });
    }

    // Operatie uitvoeren
    return await operation(revitClient);
  } finally {
    // Verbinding verbreken
    revitClient.disconnect();
    // Mutex vrijgeven zodat het volgende verzoek kan starten
    releaseMutex!();
  }
}
