import { chromium } from "@playwright/test";

const OUT_DIR = "C:/Users/zohaib.ahmed/AppData/Local/Temp/claude/E--claude-projects-NotifyHUB/7ff43c34-ba93-49b1-8020-4a9de6740143/scratchpad";

const browser = await chromium.launch();
const page = await browser.newPage({ baseURL: "http://localhost:5173" }).catch(() => null);
const context = await browser.newContext({ baseURL: "http://localhost:5173" });
const p = await context.newPage();

const consoleErrors = [];
p.on("console", (msg) => {
  if (msg.type() === "error") consoleErrors.push(msg.text());
});
p.on("pageerror", (err) => consoleErrors.push(String(err)));

await p.goto("http://localhost:5173/login");
await p.getByLabel("Username").fill("admin");
await p.getByLabel("Password").fill("AdminDev1!");
await p.getByRole("button", { name: "Sign in" }).click();
await p.waitForFunction(() => !location.pathname.includes("/login"), { timeout: 15000 });

await p.goto("http://localhost:5173/settings");
await p.waitForTimeout(1000);
console.log("CURRENT_URL:", p.url());
await p.screenshot({ path: `${OUT_DIR}/1-general-tab.png` });
console.log("BODY_SNIPPET:", (await p.locator("body").innerText()).slice(0, 500));
await p.waitForSelector("text=Find a setting", { timeout: 15000 });

// Open the search dropdown
await p.getByText("Search settings...").first().click();
await p.waitForSelector("text=No settings found", { timeout: 5000 }).catch(() => {});
await p.screenshot({ path: `${OUT_DIR}/2-dropdown-open.png` });

// Type to filter
await p.keyboard.type("reminder");
await p.waitForTimeout(300);
await p.screenshot({ path: `${OUT_DIR}/3-filtered-reminder.png` });

const visibleItems = await p.locator('[cmdk-item]').allTextContents();
console.log("VISIBLE_ITEMS_AFTER_FILTER:", JSON.stringify(visibleItems));

// Clear and search for something on a different tab
await p.keyboard.press("Control+A");
await p.keyboard.type("Task forwarding");
await p.waitForTimeout(300);
await p.screenshot({ path: `${OUT_DIR}/4-filtered-task-forwarding.png` });

const item = p.locator('[cmdk-item]', { hasText: "Task forwarding" });
await item.click();
await p.waitForTimeout(1000); // let scroll + highlight animation settle
await p.screenshot({ path: `${OUT_DIR}/5-navigated-task-tab.png` });

const activeTabText = await p.locator('[role="tab"][data-state="active"]').textContent();
console.log("ACTIVE_TAB_AFTER_CLICK:", activeTabText);

const highlighted = await p.locator("#task-forwarding").evaluate((el) => el.className);
console.log("TASK_FORWARDING_CARD_CLASS_RIGHT_AFTER_CLICK:", highlighted);

console.log("CONSOLE_ERRORS:", JSON.stringify(consoleErrors));

await browser.close();
