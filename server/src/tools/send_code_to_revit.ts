import { z } from "zod";
import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import { defineRevitTool } from "../utils/defineRevitTool.js";

const transactionModeSchema = z
  .enum(["auto", "none"])
  .default("auto")
  .describe(
    "'auto' (default): the snippet runs inside one transaction that is committed when it succeeds and rolled " +
      "back when it throws; do not start your own Transaction in this mode. 'none': no transaction is opened, " +
      "so the snippet must start and commit its own Transaction to change the model."
  );

export function registerSendCodeToRevitTool(server: McpServer) {
  defineRevitTool(server, {
    name: "send_code_to_revit",
    title: "Run C# code in Revit",
    kind: "destructive",
    description:
      "Compile and run a C# snippet inside Revit. A last resort for operations that no dedicated tool " +
      "covers: prefer the dedicated tools, and before running code that changes the model, tell the user " +
      "what it will do. The snippet becomes the body of `public static object Execute(Document document, " +
      "object[] parameters)` with the usings System, System.Linq, System.Collections.Generic, " +
      "Autodesk.Revit.DB and Autodesk.Revit.UI. Use the variables `document` (the active document; no " +
      "UIApplication or UIDocument is passed) and `parameters` (the strings from 'parameters'), and end " +
      "every code path with a `return` statement (e.g. `return null;`). The Revit API works in internal " +
      "units: lengths in feet (1 ft = 304.8 mm), angles in radians. Returns success, result (the return " +
      "value serialized to a JSON string, so return plain values, lists or anonymous objects rather than " +
      "Revit API objects) and errorMessage (compile errors, with line numbers relative to the wrapped code, " +
      "or the exception thrown at run time).",
    schema: {
      code: z
        .string()
        .describe(
          "C# statements forming the body of Execute(Document document, object[] parameters); must return a value"
        ),
      parameters: z
        .array(z.string())
        .optional()
        .describe("Optional strings passed to the snippet as the object[] `parameters`"),
      transactionMode: transactionModeSchema,
    },
    buildParams: (args) => ({
      code: args.code,
      parameters: args.parameters || [],
      transactionMode: args.transactionMode,
    }),
    formatResult: (response) => {
      // Zelfde tekst als vóór de omzetting; alleen bij success=false niet langer "successful" melden
      const prefix =
        response && response.success === false ? "Code execution failed" : "Code execution successful!";
      return `${prefix}\nResult: ${JSON.stringify(response, null, 2)}`;
    },
  });
}
