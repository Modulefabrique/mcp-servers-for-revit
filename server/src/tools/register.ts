import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import fs from "fs";
import path from "path";
import { fileURLToPath } from "url";

// Tools die tijdelijk niet geregistreerd worden (bestandsnaam zonder extensie).
// Zet een regel in commentaar om de tool weer beschikbaar te maken.
const disabledTools = new Set<string>([
  "analyze_model_statistics",
  "export_room_data",
  "get_material_quantities",
  "query_stored_data",
  "say_hello",
  "search_modules",
  "store_project_data",
  "store_room_data",
  "use_module",
  "tag_all_rooms",
  "tag_all_walls",
]);

export async function registerTools(server: McpServer) {
  // Verkrijg het directorypad van het huidige bestand
  const __filename = fileURLToPath(import.meta.url);
  const __dirname = path.dirname(__filename);

  // Lees alle bestanden in de tools-directory
  const files = fs.readdirSync(__dirname);

  // Filter .ts- of .js-bestanden, maar sluit index- en register-bestanden uit
  const toolFiles = files.filter(
    (file) =>
      (file.endsWith(".ts") || file.endsWith(".js")) &&
      file !== "index.ts" &&
      file !== "index.js" &&
      file !== "register.ts" &&
      file !== "register.js"
  );

  // Importeer en registreer elke tool dynamisch
  for (const file of toolFiles) {
    if (disabledTools.has(file.replace(/\.(ts|js)$/, ""))) {
      console.error(`Tool overgeslagen (uitgeschakeld): ${file}`);
      continue;
    }

    try {
      // Bouw het importpad op
      const importPath = `./${file.replace(/\.(ts|js)$/, ".js")}`;

      // Importeer de module dynamisch
      const module = await import(importPath);

      // Zoek en voer de registratiefunctie uit
      const registerFunctionName = Object.keys(module).find(
        (key) => key.startsWith("register") && typeof module[key] === "function"
      );

      if (registerFunctionName) {
        module[registerFunctionName](server);
        console.error(`Tool geregistreerd: ${file}`);
      } else {
        console.warn(`Waarschuwing: geen registratiefunctie gevonden in bestand ${file}`);
      }
    } catch (error) {
      console.error(`Fout bij registreren van tool ${file}:`, error);
    }
  }
}
