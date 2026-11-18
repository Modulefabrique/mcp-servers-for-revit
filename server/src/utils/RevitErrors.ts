/**
 * Foutcodes die de MCP-server aan het taalmodel teruggeeft. Elke code heeft een vaste
 * herstelhint (Engels, want bedoeld voor het model), zodat het model gericht kan reageren
 * in plaats van een losse exception-tekst te moeten interpreteren.
 */
export type RevitErrorCode =
  | "connection_failed"
  | "timeout"
  | "no_response"
  | "command_not_found"
  | "revit_error";

export const ERROR_HINTS: Record<RevitErrorCode, string> = {
  connection_failed:
    "Revit is not reachable on localhost:8080. Ask the user to open Revit and switch the MCP " +
    "server on with the MCP button on the ModuleFabrique ribbon tab, then retry.",
  timeout:
    "Revit did not start this request in time, usually because a modal dialog is open in Revit " +
    "or Revit is busy. The request was NOT executed, so nothing changed in the model. Ask the user " +
    "to close any open dialogs in Revit, then retry. For large inputs, split them into smaller batches.",
  no_response:
    "No response from Revit in time. The request may still be running in Revit and its changes may " +
    "still be applied. Before retrying a modifying tool, check the current state of the model with a " +
    "read-only tool so the change is not applied twice.",
  command_not_found:
    "The Revit add-in does not provide this command. It may be disabled in the MCP Settings window " +
    "in Revit, or the command set on this machine is outdated. Tell the user; do not try to work " +
    "around it with other tools unless the user asks.",
  revit_error:
    "Revit reported an error. Read the message, correct the input (for example verify that the " +
    "element ids exist with ai_element_filter or get_selected_elements) and retry once. If it fails " +
    "again, report the message to the user.",
};

/** Fout met een vaste code, gegooid door de verbindingslaag (SocketClient/ConnectionManager). */
export class RevitBridgeError extends Error {
  readonly code: RevitErrorCode;

  constructor(code: RevitErrorCode, message: string) {
    super(message);
    this.name = "RevitBridgeError";
    this.code = code;
  }
}

/** JSON-RPC-foutcode die de plugin teruggeeft als een command niet geregistreerd is. */
const JSON_RPC_METHOD_NOT_FOUND = -32601;

/**
 * Zet een JSON-RPC-foutantwoord van de plugin om naar een RevitBridgeError met passende code.
 * Een time-out uit de plugin betekent sinds de abandoned-regel in de commandset: het verzoek is
 * niet gestart en wordt ook later niet meer uitgevoerd.
 */
export function errorFromRpc(error: { code?: number; message?: string } | undefined): RevitBridgeError {
  const message = error?.message || "Unknown error from Revit";

  if (error?.code === JSON_RPC_METHOD_NOT_FOUND) {
    return new RevitBridgeError("command_not_found", message);
  }
  if (/time-?out|timed out/i.test(message)) {
    return new RevitBridgeError("timeout", message);
  }
  return new RevitBridgeError("revit_error", message);
}

/** Bepaalt de foutcode van een willekeurige fout. */
export function classifyError(error: unknown): { code: RevitErrorCode; message: string } {
  if (error instanceof RevitBridgeError) {
    return { code: error.code, message: error.message };
  }
  const message = error instanceof Error ? error.message : String(error);
  return { code: "revit_error", message };
}
