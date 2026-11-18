import { z } from "zod";
import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import { defineRevitTool } from "../utils/defineRevitTool.js";

// Punt in mm; de plugin verwacht { x, y, z } (JZPoint)
const pointMm = z.object({
  x: z.number().describe("X coordinate in mm"),
  y: z.number().describe("Y coordinate in mm"),
  z: z.number().describe("Z coordinate in mm"),
});

export function registerAIElementFilterTool(server: McpServer) {
  defineRevitTool(server, {
    name: "ai_element_filter",
    title: "Find elements in the model",
    kind: "read",
    description:
      "The main tool to find elements (and element types) in the whole Revit model and get their ids " +
      "for follow-up tools (get_parameters, set_parameters, operate_element, delete_element). Filter by " +
      "category (filterCategory, a BuiltInCategory name like 'OST_Walls'), by API class " +
      "(filterElementType, e.g. 'Wall'), by family type (filterFamilySymbolId), by visibility in the " +
      "active view and/or by a spatial bounding box; at least one of category, element type or family " +
      "type is required. Furniture may be in OST_Furniture or OST_GenericModel. " +
      "By default only lightweight base info is returned per element (Id, Name, FamilyName, Category, " +
      "BuiltInCategory); set includeDetails only when the question needs geometry, level or dimension " +
      "data, it makes the response much larger. " +
      "UNITS in the response: lengths, coordinates, elevations and bounding boxes in mm; room/area Area " +
      "in m2 and Volume in m3; dimension parameters as text in mm (length), m2, m3 or degrees (angle), " +
      "always with a dot as decimal separator. " +
      "PAGING: at most maxElements results per call (default 100, max 1000). The response always " +
      "contains totalMatched, returned, truncated and nextOffset; if truncated is true there are more " +
      "matches - call again with the same filter and offset=nextOffset, or tell the user how many there " +
      "are in total. For counting, totalMatched is enough (no need to fetch every page). " +
      "Example: 'Which door types are used?' -> {\"filterCategory\": \"OST_Doors\", \"includeTypes\": true, " +
      "\"includeInstances\": false, \"includeInstanceCount\": true} and keep types with InstanceCount > 0. " +
      "Example: 'Walls taller than 5 m' -> {\"filterCategory\": \"OST_Walls\", \"includeDetails\": true}, " +
      "then filter on height > 5000 mm. Changes nothing.",
    schema: {
      data: z
        .object({
          filterCategory: z
            .string()
            .optional()
            .describe("BuiltInCategory name to filter on, e.g. OST_Walls, OST_Floors, OST_Doors, OST_GenericModel"),
          filterElementType: z
            .string()
            .optional()
            .describe("Revit API class name to filter on, e.g. 'Wall', 'Floor' or 'Autodesk.Revit.DB.Wall'"),
          filterFamilySymbolId: z
            .number()
            .optional()
            .describe("ElementId of a family type (FamilySymbol): only instances of that type. Omit or -1 for no type filter."),
          includeTypes: z
            .boolean()
            .default(false)
            .describe("Include element types (wall types, door types, ...) in the results"),
          includeInstances: z
            .boolean()
            .default(true)
            .describe("Include placed element instances (walls, doors, ...) in the results"),
          filterVisibleInCurrentView: z
            .boolean()
            .optional()
            .describe("Only return instances visible in the active view (does not apply to types)"),
          boundingBoxMin: pointMm
            .optional()
            .describe("Minimum corner (mm) of a spatial filter box; must be set together with boundingBoxMax. Only elements intersecting the box are returned."),
          boundingBoxMax: pointMm
            .optional()
            .describe("Maximum corner (mm) of a spatial filter box; must be set together with boundingBoxMin."),
          maxElements: z
            .number()
            .int()
            .optional()
            .describe("Page size: maximum number of elements to return (default 100, max 1000)"),
          offset: z
            .number()
            .int()
            .optional()
            .describe("Number of matching elements to skip; for the next page use nextOffset from the previous response (default 0)"),
          includeDetails: z
            .boolean()
            .optional()
            .describe(
              "When true, returns extended information per element depending on its kind: level, bounding " +
                "box, thickness/height, type dimension parameters, room area/volume, view properties, " +
                "annotation text/dimension values, group/link info. Defaults to false: only Id, Name, " +
                "FamilyName, Category and BuiltInCategory, which keeps the response small. FamilyName is " +
                "omitted when the element itself is a family."
            ),
          includeInstanceCount: z
            .boolean()
            .optional()
            .describe(
              "Only with includeTypes: every returned type gets InstanceCount, the number of placed " +
                "instances of that type in the whole project (0 = unused). Ignores the visibility and " +
                "bounding box filters. Use with {\"includeTypes\": true, \"includeInstances\": false} to find " +
                "the types that are actually used without fetching every instance."
            ),
        })
        .describe("Filter settings; multiple filters are combined (AND). Coordinates in mm."),
    },
  });
}
