import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import type { CallToolResult, ToolAnnotations } from "@modelcontextprotocol/sdk/types.js";
import { z } from "zod";
import { withRevitConnection } from "./ConnectionManager.js";
import { ERROR_HINTS, RevitErrorCode, classifyError } from "./RevitErrors.js";

/**
 * Soort tool, bepaalt de MCP-annotaties:
 * - "read":        leest alleen, wijzigt niets in het model of de weergave.
 * - "write":       voegt iets toe of wijzigt weergave-instellingen, maar overschrijft of verwijdert
 *                  geen bestaande modelgegevens (bijv. elementen aanmaken, taggen, inkleuren).
 * - "destructive": kan bestaande gegevens overschrijven of verwijderen (verwijderen, parameters
 *                  overschrijven, willekeurige code). De client kan hierbij zelf om bevestiging vragen.
 */
export type RevitToolKind = "read" | "write" | "destructive";

export interface RevitToolDefinition<Shape extends z.ZodRawShape> {
  /** Naam van de MCP-tool. */
  name: string;
  /** Korte, leesbare titel (optioneel). */
  title?: string;
  /** Beschrijving voor het taalmodel (Engels). */
  description: string;
  /** Zod-schema van de parameters; leeg object als de tool geen parameters heeft. */
  schema: Shape;
  kind: RevitToolKind;
  /** Dezelfde aanroep twee keer heeft hetzelfde effect als één keer (alleen relevant voor write/destructive). */
  idempotent?: boolean;
  /** Naam van het JSON-RPC-command in de plugin; standaard gelijk aan `name`. */
  method?: string;
  /** Bouwt de parameters voor het command; standaard worden de tool-argumenten ongewijzigd doorgestuurd. */
  buildParams?: (args: z.objectOutputType<Shape, z.ZodTypeAny>) => unknown;
  /** Zet het antwoord van Revit om naar tekst; standaard JSON met inspringing. */
  formatResult?: (response: any, args: z.objectOutputType<Shape, z.ZodTypeAny>) => string;
  /**
   * Bouwt het volledige MCP-resultaat zelf (bijv. met een afbeelding); vervangt formatResult.
   * Een exception hierin wordt een gewoon foutresultaat.
   */
  buildResult?: (
    response: any,
    args: z.objectOutputType<Shape, z.ZodTypeAny>
  ) => CallToolResult | Promise<CallToolResult>;
}

function annotationsFor(kind: RevitToolKind, idempotent: boolean | undefined, title?: string): ToolAnnotations {
  const annotations: ToolAnnotations = {
    readOnlyHint: kind === "read",
    destructiveHint: kind === "destructive",
    idempotentHint: kind === "read" ? true : idempotent ?? false,
    // De tools werken alleen op het lokale Revit-model, niet op externe systemen
    openWorldHint: false,
  };
  if (title) {
    annotations.title = title;
  }
  return annotations;
}

/** Foutresultaat met vaste vorm: code, melding en herstelhint, plus isError. */
export function errorResult(toolName: string, error: unknown): CallToolResult {
  const { code, message } = classifyError(error);
  return errorResultFor(toolName, code, message);
}

export function errorResultFor(toolName: string, code: RevitErrorCode, message: string): CallToolResult {
  return {
    content: [
      {
        type: "text",
        text: JSON.stringify(
          { tool: toolName, error: { code, message, hint: ERROR_HINTS[code] } },
          null,
          2
        ),
      },
    ],
    isError: true,
  };
}

/**
 * Een antwoord dat Revit als "gelukt" terugstuurt maar zelf `Success: false` meldt (AIResult-vorm
 * van de creatie-commands), is voor de client ook een fout. De inhoud blijft ongewijzigd.
 */
function reportsFailure(response: any): boolean {
  if (response === null || typeof response !== "object" || Array.isArray(response)) {
    return false;
  }
  return response.Success === false || response.success === false;
}

/**
 * Registreert een tool die één command in Revit aanroept. Regelt de verbinding, de
 * foutafhandeling (isError + code + hint) en de MCP-annotaties, zodat elk toolbestand alleen
 * nog naam, beschrijving, schema en soort hoeft te bevatten.
 */
export function defineRevitTool<Shape extends z.ZodRawShape>(
  server: McpServer,
  definition: RevitToolDefinition<Shape>
): void {
  const method = definition.method ?? definition.name;

  server.registerTool(
    definition.name,
    {
      title: definition.title,
      description: definition.description,
      inputSchema: definition.schema,
      annotations: annotationsFor(definition.kind, definition.idempotent, definition.title),
    },
    // Cast nodig: TypeScript kan het conditionele callbacktype van de SDK niet oplossen voor een
    // generiek Shape. De argumenten zijn tijdens runtime al door de SDK tegen het schema gevalideerd.
    (async (args: z.objectOutputType<Shape, z.ZodTypeAny>): Promise<CallToolResult> => {
      try {
        const params = definition.buildParams ? definition.buildParams(args) : args;
        const response = await withRevitConnection((client) => client.sendCommand(method, params));
        if (definition.buildResult) {
          return await definition.buildResult(response, args);
        }
        const text = definition.formatResult
          ? definition.formatResult(response, args)
          : JSON.stringify(response, null, 2);

        const result: CallToolResult = { content: [{ type: "text", text }] };
        if (reportsFailure(response)) {
          result.isError = true;
        }
        return result;
      } catch (error) {
        return errorResult(definition.name, error);
      }
    }) as any
  );
}
