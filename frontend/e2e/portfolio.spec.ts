import {test,expect} from "@playwright/test";
import AxeBuilder from "@axe-core/playwright";
import {mkdir} from "node:fs/promises";
test.beforeEach(async({context})=>{
 await context.route("**/*",async route=>{
  const url=new URL(route.request().url());
  if(["127.0.0.1","localhost"].includes(url.hostname)||url.protocol==="data:")await route.continue();
  else await route.abort("blockedbyclient");
 });
});
async function select(page:any,id:string){await page.addInitScript((id:string)=>{if(!sessionStorage.getItem("portfolio-initialized")){localStorage.setItem("strauss-employee",id);sessionStorage.setItem("portfolio-initialized","true");}},id);}
async function send(page:any,text:string){const input=page.getByLabel("הודעה ליועץ",{exact:true});await expect(input).toBeEnabled();await input.fill(text);await page.getByRole("button",{name:"שליחת הודעה",exact:true}).click();await expect(page.locator(".typing-indicator")).toHaveCount(0);}
async function currentView(page:any){const rows=await(await page.request.get("/api/advisor/conversations/")).json();return(await page.request.get(`/api/advisor/conversations/${rows[0].id}`)).json();}
test("fictional family calculation, follow-up and reload are grounded locally",async({page},info)=>{
 await select(page,"DEMO-P02");await page.goto("/advisor");await expect(page.locator(".portfolio-banner")).toContainText("ללא AI חי");
 await send(page,"השוואה משפחתית מול HARBOR");
 const reply=page.locator(".chat-message.assistant").last();await expect(reply).not.toContainText("לא הצלחתי לבצע");
 const view=await currentView(page);const result=view.messages.at(-1).comparison;
 expect(result.current.salary.displayNetSalary).toBe(13074);expect(result.alternative.salary.displayNetSalary).toBe(12085);
 expect(result.alternative.familyEmployeeShare).toBe(800);expect(view.messages.at(-1).mode).toBe("deterministic-demo");
 await send(page,"מה חלקי בפער התקציב?");await expect(page.locator(".chat-message.assistant").last()).toContainText("800");
 await page.reload();await expect(page.locator(".chat-calculation")).toHaveCount(2);
 expect(await page.evaluate(()=>document.documentElement.scrollWidth<=window.innerWidth)).toBe(true);
 if(info.project.name==="desktop"){await mkdir("../docs/screenshots",{recursive:true});await page.screenshot({path:"../docs/screenshots/advisor.png",fullPage:true});}
});
test("original waiver reference and blocked mandatory-car request",async({page})=>{
 await select(page,"DEMO-P03");await page.goto("/advisor");await send(page,"אני רוצה לוותר על רכב");
 const result=(await currentView(page)).messages.at(-1).comparison;
 expect(result.alternative.salary.displayNetSalary).toBe(18470);expect(result.displayNetDifference).toBe(1692);
 await page.evaluate(()=>localStorage.setItem("strauss-employee","DEMO-P02"));await page.reload();
 await send(page,"אני רוצה לוותר על רכב");await expect(page.locator(".chat-message.assistant").last()).toContainText(/חובה|לא.*זכאי|אינו מאפשר|לא ניתן/);
});
test("catalog, salary comparison and screenshots use only original fictional data",async({page},info)=>{
 await select(page,"DEMO-P01");await page.goto("/");await expect(page.getByRole("heading",{level:1})).toBeVisible();
 if(info.project.name==="desktop")await page.screenshot({path:"../docs/screenshots/dashboard.png",fullPage:true});
 await page.goto("/catalog");await expect(page.getByText("PULSE",{exact:true}).first()).toBeVisible();
 await page.goto("/compare?car=DEMO-102");await expect(page.locator(".page-heading")).toBeVisible();await expect(page.getByText(/10,138/).first()).toBeVisible();
 if(info.project.name==="desktop")await page.screenshot({path:"../docs/screenshots/comparison.png",fullPage:true});
 const a11y=await new AxeBuilder({page}).withTags(["wcag2a","wcag2aa"]).analyze();expect(a11y.violations).toEqual([]);
 expect(await page.evaluate(()=>document.documentElement.scrollWidth<=window.innerWidth)).toBe(true);
});
test("private scenario preview, save and reload remain local",async({page},info)=>{
 await select(page,"DEMO-P01");await page.goto("/lab");
 const choice=page.getByLabel("רכב בתרחיש");await expect(choice).toBeVisible();await choice.selectOption("DEMO-102");
 await page.getByRole("button",{name:/חישוב|חשב|השווא/}).click();
 await expect(page.getByLabel(/שמירת/).first()).toBeVisible();await page.getByLabel(/שמירת/).first().click();
 await expect(page.getByLabel(/מחיקת/).first()).toBeVisible();
 await page.reload();await expect(page.getByLabel(/מחיקת/).first()).toBeVisible();
 const saved=await(await page.request.get("/api/advisor/scenarios/")).json();expect(saved[0].request.alternativeCarId).toBe("DEMO-102");expect(saved[0].comparison.alternative.salary.displayNetSalary).toBe(10138);
 if(info.project.name==="desktop")await page.screenshot({path:"../docs/screenshots/scenarios.png",fullPage:true});
});

test("employee confirmation and local representative response resume calculation",async({page})=>{
 await select(page,"DEMO-P02");await page.goto("/advisor");await send(page,"השוואה משפחתית מול HARBOR");
 await send(page,"נציג לאישור השתתפות");
 await page.getByRole("button",{name:"אישור העברה לנציג",exact:true}).click();
 await expect.poll(async()=>Boolean((await currentView(page)).ticket)).toBe(true);
 const before=await currentView(page);const id=before.ticket.id;const conversationId=before.conversation.id;
 await page.goto("/representative");await page.getByRole("button",{name:"כניסת נציג הדגמה",exact:true}).click();
 const ticketButton=page.locator(".ticket-row").filter({hasText:id.slice(0,8)}).first();await expect(ticketButton).toBeVisible();await ticketButton.click();
 await page.getByLabel("סכום השתתפות מאושר").fill("145");
 await page.getByLabel("תשובת נציג",{exact:true}).fill("אישור פיקטיבי מקומי בלבד");
 await page.locator("form.representative-answer button[type=submit], form.representative-answer button.primary").click();
 await expect.poll(async()=>{
  const v=await(await page.request.get(`/api/advisor/conversations/${conversationId}`)).json();return v.ticketHistory?.[0]?.ticket.status;
 }).toBe("Answered");
 const resumed=await(await page.request.get(`/api/advisor/conversations/${conversationId}`)).json();
 expect(resumed.conversation.state.confirmedParticipation).toBe(145);expect(resumed.messages.at(-1).comparison.alternative.familyEmployeeShare).toBe(800);
 await page.goto("/advisor");await expect(page.getByText(/אישור פיקטיבי מקומי בלבד/).first()).toBeVisible();
});
