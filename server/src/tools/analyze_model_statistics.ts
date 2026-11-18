import { z } from "zod";
import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import { defineRevitTool } from "../utils/defineRevitTool.js";

export function registerAnalyzeModelStatisticsTool(server: McpServer) {
  defineRevitTool(server, {
    name: "analyze_model_statistics",
    title: "Analyze model statistics",
    kind: "read",
    description:
      "Summarize the composition of the whole open Revit model (not just the active view): project name, " +
      "total number of element instances, element types, families (counted from placed family instances), " +
      "views (excluding view templates) and sheets; per category the number of instances and, when " +
      "includeDetailedTypes is true, a breakdown per family and type with instance counts (only for family " +
      "instances, so system families such as walls and floors get a count but no type breakdown); and per " +
      "level the number of elements assigned to that level. Category names are as shown in Revit's UI " +
      "language. Level 'elevation' is in Revit internal units (feet), not millimeters. Changes nothing. " +
      "Can be slow on large models; use includeDetailedTypes=false for a smaller result. Useful for a model " +
      "audit or an overview before querying specific elements with ai_element_filter.",
    schema: {
      includeDetailedTypes: z
        .boolean()
        .optional()
        .default(true)
        .describe(
          "Include the per-family/per-type instance breakdown within each category. Defaults to true."
        ),
    },
    buildParams: (args) => ({
      includeDetailedTypes: args.includeDetailedTypes ?? true,
    }),
  });
}
