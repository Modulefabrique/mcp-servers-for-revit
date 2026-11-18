import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import type { CallToolResult } from "@modelcontextprotocol/sdk/types.js";
import { z } from "zod";
import {
  getAllProjects,
  getProjectById,
  getProjectByName,
  getRoomsByProjectId,
  getAllRoomsWithProject,
  getStats
} from "../database/service.js";

// Resultaat als ingesprongen JSON; bij een fout met isError zodat de client het als mislukt ziet
function jsonResult(value: unknown, isError = false): CallToolResult {
  const result: CallToolResult = {
    content: [{ type: "text", text: JSON.stringify(value, null, 2) }],
  };
  if (isError) {
    result.isError = true;
  }
  return result;
}

export function registerQueryStoredDataTool(server: McpServer) {
  server.registerTool(
    "query_stored_data",
    {
      title: "Query stored project data",
      description:
        "Read project and room records from the MCP server's local SQLite database (data saved earlier with " +
        "store_project_data / store_room_data; this does not query Revit, so it may be outdated compared to " +
        "the model). query_type: 'all_projects' (newest first), 'project_by_id' / 'rooms_by_project_id' " +
        "(need project_id), 'project_by_name' / 'rooms_by_project_name' (need project_name, exact match), " +
        "'all_rooms' (with their project name) or 'stats' (number of projects and rooms). Returns " +
        "{ success, query_type, data }; an unknown project or a missing required argument is returned as an " +
        "error. Area and perimeter are returned exactly as they were stored.",
      inputSchema: {
        query_type: z.enum([
          "all_projects",
          "project_by_id",
          "project_by_name",
          "rooms_by_project_id",
          "rooms_by_project_name",
          "all_rooms",
          "stats"
        ]).describe("Type of query to perform"),
        project_id: z
          .number()
          .optional()
          .describe("Database project id (required for 'project_by_id' and 'rooms_by_project_id'); not a Revit element id"),
        project_name: z
          .string()
          .optional()
          .describe("Exact project name (required for 'project_by_name' and 'rooms_by_project_name')")
      },
      annotations: {
        title: "Query stored project data",
        readOnlyHint: true,
        destructiveHint: false,
        idempotentHint: true,
        // Alleen de lokale database, geen externe systemen
        openWorldHint: false,
      },
    },
    async (args): Promise<CallToolResult> => {
      try {
        let result: any;

        switch (args.query_type) {
          case "all_projects":
            result = getAllProjects();
            break;

          case "project_by_id":
            if (!args.project_id) {
              throw new Error("project_id is required for this query type");
            }
            result = getProjectById(args.project_id);
            if (!result) {
              return jsonResult({
                success: false,
                error: `Project with ID ${args.project_id} not found`
              }, true);
            }
            break;

          case "project_by_name":
            if (!args.project_name) {
              throw new Error("project_name is required for this query type");
            }
            result = getProjectByName(args.project_name);
            if (!result) {
              return jsonResult({
                success: false,
                error: `Project "${args.project_name}" not found`
              }, true);
            }
            break;

          case "rooms_by_project_id":
            if (!args.project_id) {
              throw new Error("project_id is required for this query type");
            }
            result = getRoomsByProjectId(args.project_id);
            break;

          case "rooms_by_project_name": {
            if (!args.project_name) {
              throw new Error("project_name is required for this query type");
            }
            const project = getProjectByName(args.project_name);
            if (!project) {
              return jsonResult({
                success: false,
                error: `Project "${args.project_name}" not found`
              }, true);
            }
            result = getRoomsByProjectId(project.id);
            break;
          }

          case "all_rooms":
            result = getAllRoomsWithProject();
            break;

          case "stats":
            result = getStats();
            break;

          default:
            throw new Error(`Unknown query type: ${args.query_type}`);
        }

        return jsonResult({
          success: true,
          query_type: args.query_type,
          data: result
        });
      } catch (error: any) {
        return jsonResult({
          success: false,
          error: error instanceof Error ? error.message : String(error)
        }, true);
      }
    }
  );
}
