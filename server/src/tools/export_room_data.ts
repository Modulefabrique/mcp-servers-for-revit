import { z } from "zod";
import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import { defineRevitTool } from "../utils/defineRevitTool.js";

export function registerExportRoomDataTool(server: McpServer) {
  defineRevitTool(server, {
    name: "export_room_data",
    title: "Export room data",
    kind: "read",
    description:
      "List all rooms in the project (whole model, not just the active view) with id, uniqueId, name, " +
      "number, level name, area, volume, perimeter, unbounded height, department, comments, phase and " +
      "occupancy, plus totalRooms and totalArea. Values are in Revit internal units, not metric: area and " +
      "totalArea in square feet (x 0.09290304 = m2), volume in cubic feet (x 0.0283168 = m3), perimeter and " +
      "unboundedHeight in feet (x 304.8 = mm). Rooms with zero area (unplaced or not enclosed) are only " +
      "included when both includeUnplacedRooms and includeNotEnclosedRooms are true. Changes nothing.",
    schema: {
      includeUnplacedRooms: z
        .boolean()
        .optional()
        .default(false)
        .describe(
          "Together with includeNotEnclosedRooms=true: also include rooms with zero area. Defaults to false."
        ),
      includeNotEnclosedRooms: z
        .boolean()
        .optional()
        .default(false)
        .describe(
          "Together with includeUnplacedRooms=true: also include rooms with zero area. Defaults to false."
        ),
    },
    buildParams: (args) => ({
      includeUnplacedRooms: args.includeUnplacedRooms ?? false,
      includeNotEnclosedRooms: args.includeNotEnclosedRooms ?? false,
    }),
  });
}
