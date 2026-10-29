import { test, expect } from "@playwright/test";

test("disconnected state is honest and sample is labelled", async ({ page }) => {
  await page.goto("/");
  await expect(page.locator("#stateTitle")).toHaveText("Not connected");
  await expect(page.locator("#jobName")).toHaveText("No video reported");

  await page.getByRole("button", { name: "View sample" }).click();
  await expect(page.locator("#stateTitle")).toHaveText("Sample preview");
  await expect(page.locator("#demoMarker")).toBeVisible();
  await expect(page.locator("#demoMarker")).toContainText("not your computer");
  await expect(page.locator("#progressText")).toHaveText("68%");
  await expect(page.locator("#desktopConnection")).toHaveText("Sample, not connected");

  await page.getByRole("button", { name: "Exit sample" }).click();
  await expect(page.locator("#demoMarker")).toBeHidden();
  await expect(page.locator("#stateTitle")).toHaveText("Not connected");
  await expect(page.locator("#progressText")).toHaveText("Not reported");
});

test("viewer token errors stay visible instead of falsely connecting", async ({ page }) => {
  await page.route("**/api/status", route => route.fulfill({
    status: 503, contentType: "application/json",
    body: JSON.stringify({ message: "not configured" })
  }));
  await page.goto("/");
  await page.getByRole("button", { name: "Connect dashboard" }).first().click();
  const dialog = page.locator("#connectDialog");
  await expect(dialog).toBeVisible();
  await page.locator("#viewerToken").fill("x".repeat(48));
  await page.locator("#saveConnectionBtn").click();
  await expect(dialog).toBeVisible();
  await expect(page.locator("#dialogError")).toContainText("STATUS_STORE");
  await expect(page.locator("#stateTitle")).toHaveText("Not connected");
  await page.keyboard.press("Escape");
  await expect(dialog).toBeHidden();
});

test("live and stale data show truthful states", async ({ page }) => {
  let online = true;
  await page.route("**/api/status", route => route.fulfill({
    status: 200, contentType: "application/json",
    body: JSON.stringify({
      configured: true,
      online,
      job: {
        fileName: "test_garden.mp4", jobId: "test_job",
        status: "processing", progress: 42,
        clipsDone: 4, clipsTotal: 10, fileSizeGb: 2.5,
        updatedAt: new Date().toISOString()
      }
    })
  }));
  await page.goto("/");
  await page.getByRole("button", { name: "Connect dashboard" }).first().click();
  await page.locator("#viewerToken").fill("secure-example-viewer-token-0123456789");
  await page.locator("#saveConnectionBtn").click();

  await expect(page.locator("#connectDialog")).toBeHidden();
  await expect(page.locator("#stateTitle")).toHaveText("Desktop reporting");
  await expect(page.locator("#jobName")).toHaveText("test_garden.mp4");
  await expect(page.locator("#progressText")).toHaveText("42%");
  await expect(page.locator("#jobClipsFact")).toHaveText("4 / 10");
  await expect(page.locator("#demoMarker")).toBeHidden();

  online = false;
  await page.locator("#previewBtn").click();
  await page.locator("#previewBtn").click();
  await expect(page.locator("#stateTitle")).toHaveText("Desktop not reporting");
  await expect(page.locator("#desktopConnection")).toHaveText("Not reporting");
});

test("layout has no sideways scroll and controls remain usable", async ({ page }) => {
  await page.goto("/");
  for (const width of [320, 390, 600, 820, 1040, 1440]) {
    await page.setViewportSize({ width, height: 780 });
    const overflow = await page.evaluate(() =>
      document.documentElement.scrollWidth - document.documentElement.clientWidth
    );
    expect(overflow, "horizontal overflow at " + width + "px").toBeLessThanOrEqual(1);
    await expect(page.locator("#connectBtn")).toBeVisible();
  }
  await page.setViewportSize({ width: 390, height: 780 });
  await page.locator("#setupBtn").click();
  await expect(page.locator("#viewerToken")).toBeFocused();
  await page.keyboard.press("Escape");
  await expect(page.locator("#connectDialog")).toBeHidden();
});
