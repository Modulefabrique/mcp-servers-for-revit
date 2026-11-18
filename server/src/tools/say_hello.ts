import { z } from "zod";
import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import { defineRevitTool } from "../utils/defineRevitTool.js";

export function registerSayHelloTool(server: McpServer) {
  defineRevitTool(server, {
    name: "say_hello",
    title: "Show test dialog in Revit",
    // Wijzigt niets in het model; toont alleen een dialoog
    kind: "read",
    description:
      "Test tool: show a message dialog in Revit. The dialog is modal: Revit is blocked and this call only " +
      "returns after the user clicks OK in Revit. Only use it when the user explicitly asks for a test " +
      "dialog; to check the connection without user interaction use get_current_view_info. Returns success " +
      "and the message that was shown.",
    schema: {
      message: z
        .string()
        .optional()
        .describe("Text to show in the dialog. Defaults to 'Hello MCP!'"),
    },
  });
}
