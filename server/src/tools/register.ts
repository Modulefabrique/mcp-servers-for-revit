import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import fs from "fs";
import path from "path";
import { fileURLToPath } from "url";

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
