import { z } from "zod";
import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import { defineRevitTool } from "../utils/defineRevitTool.js";

export function registerGetAvailableParametersTool(server: McpServer) {
  defineRevitTool(server, {
    name: "get_available_parameters",
    title: "List available parameters",
    kind: "read",
    description:
      "List which parameters one or more Revit elements have (names and metadata only, no values), " +
      "split into instance parameters and type parameters. Only parameters visible in Revit's " +
      "Properties panel / Type Properties dialog are included. Always pass a list of element ids, even " +
      "for a single element; take the ids from ai_element_filter, get_selected_elements or " +
      "get_current_view_elements (never invent ids). Use this first to discover parameter names, then call " +
      "get_parameters to read only the values you need, or set_parameters to change them (parameters with " +
      "IsReadOnly = true cannot be set). Result: 'Elements' holds, per element, its InstanceParameters and a " +
      "TypeId (Found = false with a Message for ids that do not exist); 'Types' holds the TypeParameters once " +
      "per unique TypeId (elements of the same type share them). If an element id is itself a type, all its " +
      "parameters appear under Types. Changes nothing.",
    schema: {
      elementIds: z
        .array(z.string().describe("Revit ElementId, as a string"))
        .min(1)
        .describe("Elements to inspect. Use a single-item list for one element."),
    },
    buildParams: (args) => ({
      elementIds: args.elementIds,
    }),
  });
}
