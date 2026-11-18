import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import type { CallToolResult } from "@modelcontextprotocol/sdk/types.js";
import { z } from "zod";
import { storeRoomsBatch, getProjectByName, getRoomsByProjectId } from "../database/service.js";

const RoomSchema = z.object({
  room_id: z.string().describe("Revit element id of the room; key of the record within the project"),
  room_name: z.string().optional().describe("Room name"),
  room_number: z.string().optional().describe("Room number"),
  department: z.string().optional().describe("Department"),
  level: z.string().optional().describe("Level name"),
  area: z.number().optional().describe("Room area; stored as given, state the unit in metadata if needed"),
  perimeter: z.number().optional().describe("Room perimeter; stored as given"),
  occupancy: z.string().optional().describe("Occupancy type"),
  comments: z.string().optional().describe("Additional comments"),
  metadata: z.record(z.string()).optional().describe("Additional room metadata as key-value pairs")
});

export function registerStoreRoomDataTool(server: McpServer) {
  server.registerTool(
    "store_room_data",
    {
      title: "Store room data",
      description:
        "Save room records for a project in the MCP server's local SQLite database (not in the Revit model) " +
        "for later retrieval with query_stored_data. The project must already exist: call store_project_data " +
        "first, otherwise an error is returned. Rooms are keyed by room_id within the project: an existing " +
        "room is updated and every optional field that is omitted is cleared, so send complete records. All " +
        "rooms of one call are stored in a single database transaction. Values are stored exactly as given " +
        "(no unit conversion); room data can be read from Revit with export_room_data (areas in square feet). " +
        "Returns { success, message, project_id, project_name, total_rooms, rooms_stored }.",
      inputSchema: {
        project_name: z.string().describe("Exact name of the stored project these rooms belong to"),
        rooms: z.array(RoomSchema).describe("Room records to store")
      },
      annotations: {
        title: "Store room data",
        readOnlyHint: false,
        destructiveHint: false,
        // Dezelfde aanroep herhalen levert dezelfde records op (alleen de tijdstempel verandert)
        idempotentHint: true,
        // Alleen de lokale database, geen externe systemen
        openWorldHint: false,
      },
    },
    async (args): Promise<CallToolResult> => {
      try {
        // Project ophalen; het moet al bestaan
        const project = getProjectByName(args.project_name);

        if (!project) {
          return {
            content: [
              {
                type: "text",
                text: JSON.stringify({
                  success: false,
                  error: `Project "${args.project_name}" not found. Please store project data first using store_project_data tool.`
                }, null, 2)
              }
            ],
            isError: true
          };
        }

        // Ruimten opslaan
        const count = storeRoomsBatch(project.id, args.rooms);
        const rooms = getRoomsByProjectId(project.id);

        return {
          content: [
            {
              type: "text",
              text: JSON.stringify({
                success: true,
                message: `Stored ${count} room(s) successfully`,
                project_id: project.id,
                project_name: args.project_name,
                total_rooms: rooms.length,
                rooms_stored: count
              }, null, 2)
            }
          ]
        };
      } catch (error: any) {
        return {
          content: [
            {
              type: "text",
              text: JSON.stringify({
                success: false,
                error: error instanceof Error ? error.message : String(error)
              }, null, 2)
            }
          ],
          isError: true
        };
      }
    }
  );
}
