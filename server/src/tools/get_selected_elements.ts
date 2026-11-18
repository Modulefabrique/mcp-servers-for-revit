import { z } from "zod";
import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import { defineRevitTool } from "../utils/defineRevitTool.js";

export function registerGetSelectedElementsTool(server: McpServer) {
  defineRevitTool(server, {
    name: "get_selected_elements",
    title: "Get selected elements",
    kind: "read",
    description:
      "Get the elements the user currently has selected in Revit. Use this when the user refers to 'the " +
      "selected elements' or 'this element'; the returned Ids can be passed to get_available_parameters, " +
      "get_parameters, set_parameters and the other element tools. Returns per element: Id, UniqueId, Name " +
      "and Category (category name in Revit's UI language); no parameter values, use get_parameters for " +
      "those. An empty list means nothing is selected: ask the user to select elements in Revit. Changes " +
      "nothing.",
    schema: {
      limit: z
        .number()
        .optional()
        .describe(
          "Maximum number of elements to return. Omit for no limit (all selected elements are returned)."
        ),
    },
    buildParams: (args) => ({
      limit: args.limit,
    }),
  });
}
