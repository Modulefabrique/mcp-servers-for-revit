import { z } from "zod";
import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import { defineRevitTool } from "../utils/defineRevitTool.js";

export function registerTagAllWallsTool(server: McpServer) {
  defineRevitTool(server, {
    name: "tag_all_walls",
    title: "Tag walls",
    kind: "write",
    method: "tag_walls",
    description:
      "Tag every wall visible in the ACTIVE view with a wall tag at the midpoint of its location line, in " +
      "one transaction. Existing tags are not checked: calling it again, or on a view that already has wall " +
      "tags, creates duplicate tags. Tag type: tagTypeId if it is a wall tag or multi-category tag type id " +
      "(from get_available_family_types with categoryList ['OST_WallTags']; never invent ids), otherwise the " +
      "first wall tag type, otherwise the first multi-category tag type. The active view must allow tags " +
      "(e.g. a floor plan); walls that cannot be tagged are listed in errors. Returns success, totalWalls, " +
      "taggedWalls, tags (id, wallId, wallName, location in feet - Revit internal units) and errors.",
    schema: {
      useLeader: z
        .boolean()
        .optional()
        .default(false)
        .describe("Give the created tags a leader line"),
      tagTypeId: z
        .string()
        .optional()
        .describe(
          "Element id of the wall tag type to use, as a string. If omitted or invalid, the first wall tag type is used"
        ),
    },
  });
}
