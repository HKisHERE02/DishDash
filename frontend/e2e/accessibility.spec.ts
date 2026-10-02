import { expect, test } from '@playwright/test'
import AxeBuilder from '@axe-core/playwright'
test('discovery, comparison and registration pass WCAG AA automated checks', async ({ page }) => {
  for (const path of ['/discover', '/dishes/1', '/register']) {
    await page.goto(path)
    await expect(page.locator('h1').first()).toBeVisible()
    if (path === '/discover') await expect(page.locator('.dish-card')).toHaveCount(20)
    if (path === '/dishes/1') await expect(page.locator('.compare-card')).toHaveCount(2)
    const results = await new AxeBuilder({ page })
      .withTags(['wcag2a', 'wcag2aa', 'wcag21aa'])
      .analyze()
    expect(
      results.violations.map((v) => ({
        rule: v.id,
        nodes: v.nodes.map((n) => ({ target: n.target, reason: n.failureSummary })),
      })),
    ).toEqual([])
  }
})
