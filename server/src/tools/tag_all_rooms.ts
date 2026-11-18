import { z } from "zod";
import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import { defineRevitTool } from "../utils/defineRevitTool.js";

export function registerTagAllRoomsTool(server: McpServer) {
  defineRevitTool(server, {
    name: "tag_all_rooms",
    title: "Tag rooms",
    kind: "write",
    // Ruimten die in het aanzicht al een label hebben worden overgeslagen
    idempotent: true,
    method: "tag_rooms",
    description:
      "Place room tags in a plan view, at each room's location point, in one transaction. Without roomIds " +
      "all rooms visible in the view are tagged; with roomIds (room element ids from ai_element_filter or " +
      "get_current_view_elements, all on the same level; never invent ids) only those. Side effect: if the " +
      "active view is not a floor or ceiling plan of the rooms' level (taken from the first roomId, or from " +
      "any placed room in the project when roomIds is omitted), the tool switches Revit's active view to " +
      "the first floor plan of that level and tags there; tell the user when viewSwitched is true. Rooms " +
      "that already have a tag in the view are skipped and unplaced or not enclosed rooms (area 0) are " +
      "ignored, so repeating the call creates no duplicate tags. Tags get Revit's default room tag type: " +
      "tagTypeId is currently not applied. Returns success, totalRooms, taggedRooms, skippedCount, tags " +
      "(tagId, roomId, roomName, roomNumber, location in mm), skippedRooms, errors, viewSwitched and message.",
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
          "Currently not applied: tags always get Revit's default room tag type"
        ),
      roomIds: z
        .array(z.number())
        .optional()
        .describe(
          "Element ids of the rooms to tag (all on the same level). If omitted, all rooms visible in the view are tagged"
        ),
    },
  });
}
