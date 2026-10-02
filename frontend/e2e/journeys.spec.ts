import { expect, test, type Page } from '@playwright/test'
async function register(page: Page) {
  const email = `meal-${crypto.randomUUID()}@example.test`
  await page.goto('/register')
  await page.getByLabel('Your name').fill('Alex')
  await page.getByLabel('Email address').fill(email)
  await page.getByLabel('Password', { exact: true }).fill('Test-Meal-2026!')
  await page.getByRole('button', { name: 'Create account' }).click()
  await expect(page).toHaveURL(/discover/)
  return email
}
async function addDish(page: Page, kind: 'Cook' | 'Restaurant') {
  await page.goto('/dishes/1')
  await page
    .getByRole('button', {
      name: kind === 'Cook' ? 'Explore grocery options' : 'Explore restaurants',
    })
    .click()
  await page
    .getByRole('button', { name: kind === 'Cook' ? 'Add grocery basket' : 'Add restaurant meal' })
    .first()
    .click()
  await expect(page).toHaveURL(/cart/)
}
test('registration, persistent session, logout and login', async ({ page }) => {
  const email = await register(page)
  await page.reload()
  await expect(page.getByRole('link', { name: 'Your profile' })).toBeVisible()
  await page.getByRole('button', { name: 'Sign out' }).click()
  await page.getByRole('link', { name: 'Sign in', exact: true }).click()
  await page.getByLabel('Email address').fill(email)
  await page.getByLabel('Password', { exact: true }).fill('Wrong-Meal-2026!')
  await page.getByRole('button', { name: 'Sign in', exact: true }).click()
  await expect(page.getByRole('alert')).toContainText('Unable to sign in')
  await page.getByLabel('Password', { exact: true }).fill('Test-Meal-2026!')
  await page.getByRole('button', { name: 'Sign in', exact: true }).click()
  await expect(page).toHaveURL(/discover/)
})
test('search, empty results, filters and servings comparison', async ({ page }) => {
  await page.goto('/discover')
  await expect(page.getByRole('heading', { name: 'Butter Chicken', exact: true })).toBeVisible()
  await page.getByRole('textbox', { name: 'Search dishes' }).fill('doesnotexist')
  await expect(page.getByRole('heading', { name: 'No dishes found' })).toBeVisible()
  await page.getByRole('button', { name: 'Clear filters' }).click()
  await page.getByRole('button', { name: 'Indian', exact: true }).click()
  await expect(page.locator('.dish-card')).toHaveCount(3)
  await page.getByRole('link', { name: 'Butter Chicken', exact: true }).click()
  await expect(page.getByRole('region', { name: 'Cook versus order comparison' })).toContainText(
    '$7.70',
  )
  await page.getByRole('button', { name: 'More servings' }).click()
  await page.getByRole('button', { name: 'More servings' }).click()
  await expect(page.getByRole('region', { name: 'Cook versus order comparison' })).toContainText(
    '$15.40',
  )
  await expect(page.locator('.ingredients')).toContainText('600 g')
  await expect(page.locator('.comparison-insight')).toContainText('Cooking is estimated to save')
})
for (const kind of ['Cook', 'Restaurant'] as const) {
  test(`${kind} demo checkout, decline recovery, tracking and completed rating`, async ({
    page,
  }) => {
    await register(page)
    await addDish(page, kind)
    await page.getByLabel('Demo payment result').selectOption('decline')
    await page.getByRole('button', { name: 'Place demo order' }).click()
    await expect(page.getByRole('alert')).toContainText('Demo payment declined')
    await expect(
      page.locator('input[type="password"], input[autocomplete="cc-number"]'),
    ).toHaveCount(0)
    await page.getByLabel('Demo payment result').selectOption('success')
    await page.getByRole('button', { name: 'Place demo order' }).click()
    await expect(page.getByRole('status')).toContainText('No payment was taken')
    await expect(page.getByRole('button', { name: 'Save rating' })).toHaveCount(0)
    for (let i = 0; i < 3; i++) {
      await page.getByRole('button', { name: 'Advance demo status' }).click()
      await expect(page.locator('.timeline .done')).toHaveCount(i + 2)
    }
    await page.getByLabel('Comment (optional)').fill('A lovely meal')
    await page.getByRole('button', { name: 'Save rating' }).click()
    await expect(page.getByRole('button', { name: 'Update rating' })).toBeVisible()
    await page.getByLabel('Your rating').selectOption('4')
    await page.getByRole('button', { name: 'Update rating' }).click()
    await page.getByRole('button', { name: 'Delete rating' }).click()
    await expect(page.getByRole('button', { name: 'Save rating' })).toBeVisible()
    await page.getByRole('link', { name: 'Your orders', exact: true }).click()
    await expect(page.locator('.order-preview')).toHaveCount(1)
    await page.getByRole('link', { name: /Notifications/ }).click()
    await expect(page.locator('.notification')).toHaveCount(4)
    await page.getByRole('button', { name: 'Mark read' }).first().click()
    await expect(page.getByRole('button', { name: 'Mark read' })).toHaveCount(3)
  })
}
test('favorites and allergen-aware recommendations', async ({ page }) => {
  await register(page)
  await page.getByRole('button', { name: 'Save Butter Chicken', exact: true }).click()
  await page.getByRole('link', { name: 'Favourites', exact: true }).click()
  await expect(page.locator('.dish-card')).toHaveCount(1)
  await page.getByRole('button', { name: 'Unsave Butter Chicken' }).click()
  await expect(page.getByRole('heading', { name: 'Your recipe box is waiting' })).toBeVisible()
  await page.getByRole('link', { name: 'Your profile' }).click()
  await page.getByLabel('milk', { exact: true }).check()
  await page.getByRole('button', { name: 'Save preferences' }).click()
  await expect(page.getByRole('status')).toContainText('Preferences saved')
  await page.getByRole('link', { name: 'For you', exact: true }).click()
  await expect(page.locator('.dish-card')).toHaveCount(8)
  await expect(page.getByRole('heading', { name: 'Butter Chicken', exact: true })).toHaveCount(0)
  await addDishExpectAllergenError(page)
})
async function addDishExpectAllergenError(page: Page) {
  await page.goto('/dishes/1')
  await page.getByRole('button', { name: 'Explore grocery options' }).click()
  await page.getByRole('button', { name: 'Add grocery basket' }).first().click()
  await expect(page.getByRole('alert')).toContainText('allergen excluded')
}
test('guided cooking restores progress and timer after reload', async ({ page }) => {
  await page.goto('/dishes/1/cook')
  await page.getByRole('button', { name: 'Next step' }).click()
  await page.getByRole('button', { name: 'Start 2 minute timer' }).click()
  await page.reload()
  await expect(page.locator('.active-step')).toContainText('STEP 2 OF 4')
  await expect(page.getByRole('status', { name: 'Timer remaining' })).toBeVisible()
  await page.getByRole('button', { name: 'Reset timer' }).click()
  await page.getByRole('button', { name: 'Previous' }).click()
  await expect(page.locator('.active-step')).toContainText('STEP 1 OF 4')
  for (let i = 0; i < 3; i++) await page.getByRole('button', { name: 'Next step' }).click()
  await page.getByRole('button', { name: 'Finish cooking' }).click()
  await expect(page.getByRole('heading', { name: 'Made by you. Enjoy!' })).toBeVisible()
})
test('network errors are visible and retry recovers', async ({ page }) => {
  await page.route('**/api/dishes?*', (route) => route.abort('failed'))
  await page.goto('/discover')
  await expect(page.getByRole('alert')).toContainText('Unable to connect')
  await page.unroute('**/api/dishes?*')
  await page.getByRole('button', { name: 'Try again' }).click()
  await expect(page.locator('.dish-card')).toHaveCount(20)
})
test('mobile layout, keyboard skip link and discovery journey', async ({ page }) => {
  await page.setViewportSize({ width: 390, height: 844 })
  await page.goto('/discover')
  await page.keyboard.press('Tab')
  await expect(page.getByRole('link', { name: 'Skip to content' })).toBeFocused()
  await page.keyboard.press('Enter')
  await expect(page.locator('#main-content')).toBeFocused()
  await expect(page.locator('.dish-card')).toHaveCount(20)
  expect(await page.evaluate(() => document.documentElement.scrollWidth <= window.innerWidth)).toBe(
    true,
  )
  await page.getByRole('link', { name: 'Butter Chicken', exact: true }).click()
  await page.getByRole('button', { name: 'More servings' }).click()
  await expect(page.getByRole('region', { name: 'Cook versus order comparison' })).toContainText(
    '3 servings',
  )
  expect(await page.evaluate(() => document.documentElement.scrollWidth <= window.innerWidth)).toBe(
    true,
  )
})
test('desktop screenshot and missing dish feedback', async ({ page }) => {
  await page.setViewportSize({ width: 1440, height: 1100 })
  await page.goto('/discover')
  await expect(page.locator('.dish-card')).toHaveCount(20)
  await page.screenshot({ path: 'test-results/discover-desktop.png' })
  await page.goto('/dishes/1')
  await expect(page.locator('.compare-card')).toHaveCount(2)
  await page.screenshot({ path: 'test-results/comparison-desktop.png' })
  await page.setViewportSize({ width: 390, height: 844 })
  await page.goto('/discover')
  await expect(page.locator('.dish-card')).toHaveCount(20)
  await page.screenshot({ path: 'test-results/discover-mobile.png' })
  await page.goto('/dishes/999')
  await expect(page.getByRole('alert')).toContainText('Dish not found')
})
test('registration can be completed entirely by keyboard', async ({ page }) => {
  await page.goto('/register')
  await page.keyboard.press('Tab')
  await expect(page.getByRole('link', { name: 'Skip to content' })).toBeFocused()
  await page.keyboard.press('Enter')
  await page.keyboard.press('Tab')
  await expect(page.getByLabel('Your name')).toBeFocused()
  await page.keyboard.type('Taylor')
  await page.keyboard.press('Tab')
  await page.keyboard.type(`keyboard-${crypto.randomUUID()}@example.test`)
  await page.keyboard.press('Tab')
  await page.keyboard.type('Keyboard-Meal-2026!')
  await page.keyboard.press('Tab')
  await expect(page.getByRole('button', { name: 'Create account' })).toBeFocused()
  await page.keyboard.press('Enter')
  await expect(page).toHaveURL(/discover/)
})
test('mobile account controls and profile remain accessible', async ({ page }) => {
  await page.setViewportSize({ width: 390, height: 844 })
  await register(page)
  await page.getByRole('link', { name: 'Your profile' }).click()
  await expect(page.getByRole('heading', { name: 'Your preferences' })).toBeVisible()
  expect(await page.evaluate(() => document.documentElement.scrollWidth <= window.innerWidth)).toBe(
    true,
  )
  await page.getByRole('button', { name: 'Sign out' }).click()
  await expect(page.getByRole('link', { name: 'Sign in', exact: true })).toBeVisible()
})
