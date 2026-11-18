import { z } from "zod";
import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import { defineRevitTool } from "../utils/defineRevitTool.js";

export function registerGetCurrentViewElementsTool(server: McpServer) {
  defineRevitTool(server, {
    name: "get_current_view_elements",
    title: "List elements in the active view",
    kind: "read",
    description:
      "List the elements visible in the ACTIVE Revit view (check which view with get_current_view_info). " +
      "Filter with modelCategoryList and/or annotationCategoryList (BuiltInCategory names such as " +
      "'OST_Walls', 'OST_Doors', 'OST_Dimensions'); with both lists omitted, all element instances in the " +
      "view are returned. Hidden elements are excluded unless includeHidden is true. Per element: Id, " +
      "UniqueId, Name, Category and Properties - location in mm (LocationX/Y/Z for point-based elements, " +
      "Start/End/Length for line-based elements like walls) and the Comments, Mark, Level, Family and " +
      "Type parameters when present. To query the whole model instead of one view, use ai_element_filter. " +
      "PAGING: at most 'limit' elements are returned per call (default 100, max 1000). The response " +
      "always contains totalMatched, returned, truncated and nextOffset; if truncated is true, call again " +
      "with offset=nextOffset to get the next page. Changes nothing.",
    schema: {
      modelCategoryList: z
        .array(z.string())
        .optional()
        .describe("Model categories to include, as BuiltInCategory names (e.g. 'OST_Walls', 'OST_Doors', 'OST_Floors')"),
      annotationCategoryList: z
        .array(z.string())
        .optional()
        .describe("Annotation categories to include, as BuiltInCategory names (e.g. 'OST_Dimensions', 'OST_WallTags', 'OST_TextNotes')"),
      includeHidden: z
        .boolean()
        .optional()
        .describe("Also return elements that are hidden in the view (default false)"),
      limit: z
        .number()
        .int()
        .optional()
        .describe("Page size: maximum number of elements to return (default 100, max 1000)"),
      offset: z
        .number()
        .int()
        .optional()
        .describe("Number of matching elements to skip, for the next page use nextOffset from the previous response (default 0)"),
    },
    buildParams: (args) => ({
      modelCategoryList: args.modelCategoryList || [],
      annotationCategoryList: args.annotationCategoryList || [],
      includeHidden: args.includeHidden || false,
      limit: args.limit,
      offset: args.offset,
    }),
  });
}
