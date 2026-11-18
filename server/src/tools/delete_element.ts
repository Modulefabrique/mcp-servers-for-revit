import { z } from "zod";
import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import { defineRevitTool } from "../utils/defineRevitTool.js";

export function registerDeleteElementTool(server: McpServer) {
  defineRevitTool(server, {
    name: "delete_element",
    title: "Delete elements",
    kind: "destructive",
    description:
      "Delete one or more elements from the Revit model by their element ids. Take the ids from " +
      "ai_element_filter, get_selected_elements or get_current_view_elements; never invent ids. " +
      "SAFE BY DEFAULT: dryRun defaults to true, which deletes nothing and returns a preview: the " +
      "requested elements (id, name, category) plus the elements Revit would delete along with them " +
      "(dependentCount/dependents, e.g. doors and windows hosted in a wall, tags, dimensions). " +
      "Workflow: 1) call with dryRun omitted, 2) show the preview to the user and ask for explicit " +
      "confirmation, 3) only then call again with the same ids and dryRun=false to actually delete. " +
      "Ids that do not exist are reported in 'warning' and skipped. The real delete runs as one Revit " +
      "transaction (undoable with Ctrl+Z in Revit) and returns the total number of deleted elements, " +
      "including dependents.",
    schema: {
      elementIds: z
        .array(z.string())
        .describe("Revit element ids of the elements to delete, as strings"),
      dryRun: z
        .boolean()
        .default(true)
        .describe(
          "true (default): only preview what would be deleted, nothing changes. false: really delete " +
            "- only after the user has confirmed the preview."
        ),
    },
    buildParams: (args) => ({
      elementIds: args.elementIds,
      dryRun: args.dryRun,
    }),
  });
}
