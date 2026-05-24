import { chromium } from 'playwright';
import fs from 'fs';

const BASE_URL = 'http://localhost:5000';
const screenshotDir = './test-screenshots';

if (!fs.existsSync(screenshotDir)) {
  fs.mkdirSync(screenshotDir, { recursive: true });
}

async function takeScreenshot(page, name) {
  const path = `${screenshotDir}/${name}.png`;
  await page.screenshot({ path, fullPage: false });
  console.log(`✅ Screenshot saved: ${path}`);
}

async function runTests() {
  const browser = await chromium.launch({ headless: false });
  const context = await browser.newContext();
  const page = await context.newPage();

  try {
    console.log('\n🔍 Test 1: Login to admin panel');
    await page.goto(`${BASE_URL}/Login`);
    await page.fill('[name="Username"]', 'admin');
    await page.fill('[name="Password"]', 'FireStop@Admin123');
    await page.click('button[type="submit"]');
    await page.waitForURL(`${BASE_URL}/Jobs`);
    console.log('✅ Logged in successfully');

    console.log('\n🔍 Test 2: Find a job with PDF');
    await page.goto(`${BASE_URL}/Jobs`);
    await page.waitForSelector('.job-row a');
    // Click the first job
    const jobLinks = await page.locator('.job-row a').first();
    const href = await jobLinks.getAttribute('href');
    console.log(`✅ Found job link: ${href}`);
    await page.goto(`${BASE_URL}${href}`);
    await page.waitForSelector('h1', { timeout: 5000 });
    await takeScreenshot(page, '01-job-details');

    console.log('\n🔍 Test 3: Check for PDF and generate share code');
    // Look for Send to Customer section
    const hasPdf = await page.isVisible('.pdf-section');
    if (!hasPdf) {
      console.log('⚠️ No PDF section found, creating a test PDF...');
      // Skip for now - we'll test with existing job
    }

    // Generate share code
    const customerEmailInput = await page.$('#customerEmail');
    if (customerEmailInput) {
      await page.fill('#customerEmail', 'testclient@example.com');
      await page.click('#sendToCustomerBtn');
      await page.waitForSelector('#sendResult', { timeout: 10000 });
      const resultText = await page.textContent('#sendResult');
      console.log(`✅ Send to customer result: ${resultText}`);
      await page.waitForTimeout(1000);
    }

    // Get the share code from the page - need to reload to see generated link
    await page.reload();
    await page.waitForSelector('h1', { timeout: 5000 });

    // Find approval link - it should be in the "Approval Status" section
    const jobId = href.split('/').pop();

    // Navigate to Jobs to find the approval link
    await page.goto(`${BASE_URL}/Jobs`);
    await page.waitForSelector('.job-row', { timeout: 5000 });

    // Find and click job again
    const jobRow = page.locator('.job-row').filter({ hasText: jobId });
    const link = jobRow.locator('a').first();
    await link.click();
    await page.waitForSelector('h1', { timeout: 5000 });

    // Check if approval link is now available
    const approvalSection = await page.$('text="Approval Status"');
    await takeScreenshot(page, '02-job-with-approval');

    console.log('\n🔍 Test 4: Access JobApprove page directly');
    // We'll construct a test approval - first get the job ID and create approval
    const shareCodeElement = await page.$('text*ShareCode') || await page.$('[data-share-code]');

    // For now, let's create a known share code by querying the database
    // Actually, let's just navigate assuming we have a job with PDF

    // Try to find an existing approval or create one
    // Query the API to get a valid share code
    const jobsResponse = await page.goto(`${BASE_URL}/api/jobs`);
    console.log('Jobs API endpoint would require authentication');

    // Let's take a different approach - use the browser console to check if we have the markup feature
    const canvasExists = await page.evaluate(() => {
      return document.getElementById('drawingCanvas') !== null;
    });

    if (!canvasExists) {
      console.log('⚠️ No drawing canvas on current page. Need to access JobApprove page with share code');
      console.log('Testing markup UI indirectly...');
    }

    console.log('\n🔍 Test 5: Test Fabric.js availability');
    const fabricAvailable = await page.evaluate(() => {
      return typeof fabric !== 'undefined';
    });
    console.log(`Fabric.js available: ${fabricAvailable}`);

    console.log('\n✅ Verification completed - app is running and accessible');
    console.log(`✅ Login works`);
    console.log(`✅ Jobs page loads`);
    console.log(`✅ Job details page loads`);

    // Summary
    console.log('\n📊 SUMMARY: App structure verified. Markup feature requires valid share code.');
    console.log('To fully test markup, a job with PDF and valid share code is needed.');

    await takeScreenshot(page, '03-final-state');

  } catch (error) {
    console.error('❌ Test failed:', error.message);
    await takeScreenshot(page, 'error');
    process.exit(1);
  } finally {
    await browser.close();
  }
}

runTests();
