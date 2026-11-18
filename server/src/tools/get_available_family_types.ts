import { z } from "zod";
import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import { defineRevitTool } from "../utils/defineRevitTool.js";

export function registerGetAvailableFamilyTypesTool(server: McpServer) {
  defineRevitTool(server, {
    name: "get_available_family_types",
    title: "List family types",
    kind: "read",
    description:
      "List the element types available in the project: all loadable family types (doors, windows, " +
      "furniture, columns, structural framing, tags, ...) plus wall, floor, roof, ceiling and curtain system " +
      "types. Other system family types (e.g. duct or pipe types) are not listed. Use this to get a valid " +
      "typeId before calling create_point_based_element, create_line_based_element or " +
      "create_surface_based_element, or a tag type id for tagging; never invent type ids. Returns per type: " +
      "FamilyTypeId, UniqueId, FamilyName, TypeName and Category (category name in Revit's UI language). " +
      "Filter with categoryList (BuiltInCategory names; names that are not valid are ignored, and if none is " +
      "valid no category filter is applied) and familyNameFilter; use limit to cap the result size in large " +
      "projects. Changes nothing.",
    schema: {
      categoryList: z
        .array(z.string())
        .optional()
        .describe(
          "BuiltInCategory names to filter by (e.g. 'OST_Walls', 'OST_Doors', 'OST_Furniture', 'OST_StructuralFraming')"
        ),
      familyNameFilter: z
        .string()
        .optional()
        .describe("Case-insensitive text that must occur in the family name or the type name"),
      limit: z
        .number()
        .optional()
        .describe(
          "Maximum number of family types to return. Omit for no limit (all matching family types are returned)."
        ),
    },
    buildParams: (args) => ({
      categoryList: args.categoryList || [],
      familyNameFilter: args.familyNameFilter || "",
      limit: args.limit,
    }),
  });
}
