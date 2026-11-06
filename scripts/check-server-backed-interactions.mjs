import { readFile, readdir } from "node:fs/promises";
import { basename, dirname, extname, relative, resolve } from "node:path";
import { fileURLToPath } from "node:url";

const root = resolve(dirname(fileURLToPath(import.meta.url)), "..");
const applicationRoot = resolve(root, "web/src/app");
const apiRoot = resolve(root, "src/ConstructOPX.Api");
const sourceExtensions = new Set([".html", ".ts"]);
const sources = [];

async function collect(directory) {
  for (const entry of await readdir(directory, { withFileTypes: true })) {
    const path = resolve(directory, entry.name);
    if (entry.isDirectory()) {
      await collect(path);
    } else if (sourceExtensions.has(extname(entry.name))) {
      sources.push([relative(root, path).replaceAll("\\", "/"), await readFile(path, "utf8")]);
    }
  }
}

await collect(applicationRoot);
await collect(apiRoot);

const failures = [];
const prohibitedProductPatterns = [
  [/\bMockOperation(?:Controller|Scenario|State)?\b/, "mock-operation runtime"],
  [/\bmock-product\b/i, "mock product fixture"],
  [/(?<!\.)\bprototype\b/i, "prototype product copy"],
  [/\/api\/(?:ui|modules)(?:\/|['"`])/, "generic placeholder API"],
];
const actionLikeLabel =
  /\b(?:view(?:\s+all)?|open|search|download|export|copy|refresh|retry|save|create|submit|send|request|approve|reject|delete|remove|resume|continue|start|run|resolve|review|record|manage|long-term)\b/i;

function visibleButtonLabel(body) {
  return body
    .replace(/\{\{[\s\S]*?\}\}/g, " ")
    .replace(/<[^>]+>/g, " ")
    .replace(/\s+/g, " ")
    .trim();
}

function clickExpression(attributes) {
  const doubleQuoted = attributes.match(/\(click\)\s*=\s*"([^"]*)"/i)?.[1];
  if (doubleQuoted !== undefined) return doubleQuoted.trim();
  return attributes.match(/\(click\)\s*=\s*'([^']*)'/i)?.[1]?.trim() ?? "";
}

function isToastOnlyAction(expression) {
  const normalized = expression.replace(/\s+/g, " ").trim();
  return (
    /^(?:showToast|toast\.set)\s*\(/.test(normalized) &&
    !normalized.includes(";") &&
    !/\b(?:navigate|open|showModal|submit|save|create|update|delete|remove|download|export)\b/i.test(
      normalized.replace(/^(?:showToast|toast\.set)/, ""),
    )
  );
}

for (const [path, source] of sources) {
  if (/\uFFFD|\u00C2|\u00C3|\u00E2[\u0080-\u00BF]/.test(source)) {
    failures.push(`${path}: replacement or mojibake character`);
  }

  for (const [pattern, description] of prohibitedProductPatterns) {
    if (pattern.test(source)) failures.push(`${path}: ${description}`);
  }

  if (path.startsWith("web") && source.includes("HttpClient")) {
    const file = basename(path);
    const isCompositionRoot = path === "web/src/app/app.config.ts";
    const isTest = file.endsWith(".spec.ts");
    if (
      !file.endsWith("-api.ts") &&
      !file.endsWith(".api.ts") &&
      !file.endsWith("-api.spec.ts") &&
      !isCompositionRoot &&
      !isTest
    ) {
      failures.push(`${path}: transport logic outside a feature API`);
    }
  }

  if (path.endsWith(".html")) {
    if (
      /<(?:label|input|option)[^>]*>[^<]*\b(?:object|storage) key\b/i.test(
        source,
      )
    ) {
      failures.push(`${path}: internal storage key exposed in UI`);
    }
    if (/\$any\s*\(/.test(source)) {
      failures.push(`${path}: untyped template cast`);
    }
    if (
      /\{\{\s*(?:item\.)?icon\s*\}\}|(?:item\.)?label\.slice\(\s*0\s*,\s*1\s*\)/.test(
        source,
      )
    ) {
      failures.push(`${path}: navigation abbreviation/icon placeholder`);
    }

    // A toast is feedback, not the implementation of an action. This catches the class of defect
    // where controls such as "View all photos" or "Long-term" look actionable but only display
    // explanatory text and leave the promised product state unchanged.
    for (const match of source.matchAll(/<button\b([^>]*)>([\s\S]*?)<\/button>/gi)) {
      const attributes = match[1] ?? "";
      const label = visibleButtonLabel(match[2] ?? "");
      const expression = clickExpression(attributes);
      if (label && actionLikeLabel.test(label) && expression && isToastOnlyAction(expression)) {
        failures.push(`${path}: action-like button "${label}" is wired only to toast feedback`);
      }
    }

    const customDialogs =
      source.match(
        /<(?!dialog\b)[a-z][^>]*role=["'](?:dialog|alertdialog)["'][^>]*>/gis,
      ) ?? [];
    for (const dialog of customDialogs) {
      if (
        !/\bappAccessibleDialog\b/.test(dialog) ||
        !/\(dismissed\)=/.test(dialog)
      ) {
        failures.push(
          `${path}: custom dialog missing shared focus management or Escape dismissal`,
        );
      }
    }
  }
}

if (failures.length) {
  console.error(
    `Server-backed interaction check failed:\n${failures.map((value) => `- ${value}`).join("\n")}`,
  );
  process.exit(1);
}

console.log(
  `Server-backed interaction check passed across ${sources.length} Angular and API source files.`,
);
