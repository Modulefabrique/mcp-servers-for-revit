import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import type { CallToolResult } from "@modelcontextprotocol/sdk/types.js";
import { z } from "zod";
import { storeProject, getProjectByName } from "../database/service.js";

export function registerStoreProjectDataTool(server: McpServer) {
  server.registerTool(
    "store_project_data",
    {
      title: "Store project data",
      description:
        "Save project metadata in the MCP server's local SQLite database (not in the Revit model) for later " +
        "retrieval with query_stored_data. Records are keyed by exact project_name: an existing record with " +
        "that name is updated, and on update every optional field that is omitted is cleared, so always " +
        "send the complete set of values. Must be called before store_room_data for the same project_name. " +
        "Returns { success, message, project_id, project } with the stored record.",
      inputSchema: {
        project_name: z.string().describe("Project name; the key of the record (exact match)"),
        project_path: z.string().optional().describe("File path of the project"),
        project_number: z.string().optional().describe("Project number or identifier"),
        project_address: z.string().optional().describe("Project address or location"),
        client_name: z.string().optional().describe("Client name"),
        project_status: z.string().optional().describe("Project status (e.g. Active, Completed, On Hold)"),
        author: z.string().optional().describe("Project author or creator"),
        metadata: z.record(z.string()).optional().describe("Additional project metadata as key-value pairs")
      },
      annotations: {
        title: "Store project data",
        readOnlyHint: false,
        destructiveHint: false,
        // Dezelfde aanroep herhalen levert hetzelfde record op (alleen de tijdstempel verandert)
        idempotentHint: true,
        // Alleen de lokale database, geen externe systemen
        openWorldHint: false,
      },
    },
    async (args): Promise<CallToolResult> => {
      try {
        const projectId = storeProject(args);
        const project = getProjectByName(args.project_name);

        return {
          content: [
            {
              type: "text",
              text: JSON.stringify({
                success: true,
                message: "Project data stored successfully",
                project_id: projectId,
                project
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
