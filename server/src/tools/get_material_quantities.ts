import { z } from "zod";
import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import { defineRevitTool } from "../utils/defineRevitTool.js";

export function registerGetMaterialQuantitiesTool(server: McpServer) {
  defineRevitTool(server, {
    name: "get_material_quantities",
    title: "Get material quantities",
    kind: "read",
    description:
      "Calculate material quantities: for every material used by the analyzed elements its materialId, " +
      "materialName, materialClass, area, volume, elementCount and elementIds, plus totalMaterials, " +
      "totalArea and totalVolume. Values are in Revit internal units, not metric: area in square feet " +
      "(x 0.09290304 = m2), volume in cubic feet (x 0.0283168 = m3). Analyzes all element instances in the " +
      "project, optionally limited to categoryFilters, or only the elements currently selected in Revit " +
      "(selectedElementsOnly=true; categoryFilters is then ignored). Category names that are not valid " +
      "BuiltInCategory names are ignored; if none is valid, all categories are analyzed. Can be slow on " +
      "large projects. Changes nothing.",
    schema: {
      categoryFilters: z
        .array(z.string())
        .optional()
        .describe(
          "BuiltInCategory names to analyze (e.g. ['OST_Walls', 'OST_Floors', 'OST_Roofs']). If omitted, all categories are included"
        ),
      selectedElementsOnly: z
        .boolean()
        .optional()
        .default(false)
        .describe(
          "Only analyze the elements currently selected in Revit (categoryFilters is then ignored). Defaults to false"
        ),
    },
    buildParams: (args) => ({
      categoryFilters: args.categoryFilters ?? null,
      selectedElementsOnly: args.selectedElementsOnly ?? false,
    }),
  });
}
