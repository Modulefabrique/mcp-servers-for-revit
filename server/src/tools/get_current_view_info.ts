import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import { defineRevitTool } from "../utils/defineRevitTool.js";

export function registerGetCurrentViewInfoTool(server: McpServer) {
  defineRevitTool(server, {
    name: "get_current_view_info",
    kind: "read",
    description:
      "Get information about the active view in Revit: id, unique id, name, view type, whether it is a " +
      "view template, scale and detail level. Call this first in a session to check that Revit is connected " +
      "and to learn which view the user is working in; several tools (get_current_view_elements, " +
      "tagging, dimensions, colouring) act on this active view.",
    schema: {},
  });
}
