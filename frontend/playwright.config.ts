import { defineConfig, devices } from "@playwright/test";
import { existsSync } from "node:fs";
const apiPort = process.env.PORTFOLIO_API_PORT ?? "5080";
const uiPort = process.env.PORTFOLIO_UI_PORT ?? "5173";
for(const port of [apiPort,uiPort]) if(!/^\d+$/.test(port)||Number(port)<1024||Number(port)>65535) throw new Error("Invalid local test port");
export default defineConfig({
 testDir:"./e2e",workers:1,fullyParallel:false,timeout:45000,expect:{timeout:15000},reporter:"list",
 use:{baseURL:`http://127.0.0.1:${uiPort}`,trace:"off",launchOptions:{executablePath:process.env.PLAYWRIGHT_CHROMIUM_EXECUTABLE ?? (existsSync("/usr/bin/chromium")?"/usr/bin/chromium":undefined),args:["--no-sandbox"]}},
 projects:[{name:"desktop",use:{...devices["Desktop Chrome"],viewport:{width:1440,height:1000}}},{name:"mobile",use:{...devices["iPhone 13"],defaultBrowserType:"chromium"}}],
 webServer:[
  {command:"dotnet run --no-build --project ../backend/StraussDrive.Api",env:{PORTFOLIO_API_PORT:apiPort,ASPNETCORE_ENVIRONMENT:"Development",Advisor__DemoAuthEnabled:"true",Advisor__DatabasePath:`/tmp/portfolio-browser-${process.pid}.db`,DEMO_GATE_ENABLED:"false",Advisor__DemoRepresentativeCode:""},url:`http://127.0.0.1:${apiPort}/api/health`,reuseExistingServer:false,timeout:60000},
  {command:`npm run dev -- --port ${uiPort} --strictPort`,env:{PORTFOLIO_API_PORT:apiPort},url:`http://127.0.0.1:${uiPort}`,reuseExistingServer:false,timeout:60000}
 ]
});
