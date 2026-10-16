import {useEffect,useState,type FormEvent} from "react";
import {login,changePassword} from "../api/auth.api";
import {get,post,put,del} from "../api/client";
import {myExams,myAttempts,notifications,markNotificationRead,startExam} from "../api/portal.api";
import {students,teachers,departments,exams} from "../api/management.api";
import type {Role,Student,Teacher,Department,Exam,Notification,LoginResponse} from "../types";

function Login({onLogin}:{onLogin:(x:LoginResponse)=>void}){
 const [username,setUsername]=useState(""),[password,setPassword]=useState(""),[error,setError]=useState(""),[busy,setBusy]=useState(false);
 async function submit(e:FormEvent){e.preventDefault();setError("");setBusy(true);try{onLogin(await login(username,password))}catch(e){setError(e instanceof Error?e.message:"Login failed")}finally{setBusy(false)}}
 return <main className="auth"><form className="card auth-card" onSubmit={submit}><h1>MFT</h1><p>سامانه مدیریت آموزشی</p><label>نام کاربری<input value={username} onChange={e=>setUsername(e.target.value)} required autoComplete="username"/></label><label>رمز عبور<input type="password" value={password} onChange={e=>setPassword(e.target.value)} required autoComplete="current-password"/></label>{error&&<div className="error">{error}</div>}<button disabled={busy}>{busy?"در حال ورود...":"ورود"}</button></form></main>;
}
function ChangePassword({onDone}:{onDone:()=>void}){const [c,setC]=useState(""),[n,setN]=useState(""),[e,setE]=useState("");async function go(x:FormEvent){x.preventDefault();setE("");try{await changePassword(c,n);onDone()}catch(e){setE(e instanceof Error?e.message:"خطا")}}return <main className="auth"><form className="card auth-card" onSubmit={go}><h2>تغییر رمز عبور</h2><label>رمز فعلی<input type="password" value={c} onChange={e=>setC(e.target.value)} required/></label><label>رمز جدید<input type="password" minLength={8} value={n} onChange={e=>setN(e.target.value)} required/></label>{e&&<div className="error">{e}</div>}<button>ذخیره</button></form></main>}
function Table({title,rows}:{title:string;rows:Array<Record<string,unknown>>}){return <section className="card"><h2>{title}</h2>{rows.length===0?<p className="muted">داده‌ای وجود ندارد.</p>:<div className="table-wrap"><table><thead><tr>{Object.keys(rows[0]).map(k=><th key={k}>{k}</th>)}</tr></thead><tbody>{rows.map((r,i)=><tr key={i}>{Object.values(r).map((v,j)=><td key={j}>{typeof v==="object"&&v!==null?JSON.stringify(v):String(v??"-")}</td>)}</tr>)}</tbody></table></div>}</section>}
function Dashboard({role}:{role:Role}){const [data,setData]=useState<any>();const [error,setError]=useState("");useEffect(()=>{get("/dashboard").then(setData).catch(e=>setError(e.message))},[]);return <><header><h1>داشبورد</h1><span>{role}</span></header>{error&&<div className="error">{error}</div>}<div className="grid">{["students","teachers","departments","questions","exams"].map(k=><div className="card stat" key={k}><strong>{data?.[k]??"—"}</strong><span>{k}</span></div>)}</div></>}
function StudentPortal(){
 const [es,setEs]=useState<any[]>([]),[as,setAs]=useState<any[]>([]),[ns,setNs]=useState<Notification[]>([]),[err,setErr]=useState(""),[notice,setNotice]=useState("");
 const [active,setActive]=useState<any>(null),[answers,setAnswers]=useState<Record<string,{selectedOptionId?:string;essayText?:string}>>({});
 async function load(){try{const [e,a,n]=await Promise.all([myExams(),myAttempts(),notifications()]);setEs(e);setAs(a);setNs(n)}catch(e){setErr(e instanceof Error?e.message:"خطا")}}
 useEffect(()=>{load()},[]);
 async function begin(id:string){setErr("");setNotice("");try{const x=await startExam(id);setActive(x);setAnswers({})}catch(e){setErr(e instanceof Error?e.message:"خطا")}}
 async function submit(){if(!active)return;setErr("");try{const result=await post<any>(`/portal/attempts/${active.attemptId}/submit`,{answers:Object.entries(answers).map(([questionId,a])=>({questionId,selectedOptionId:a.selectedOptionId||null,essayText:a.essayText||null}))});setNotice(result.pendingManualGrading?"پاسخ‌ها ثبت شد؛ سؤال تشریحی نیازمند تصحیح استاد است.":`پاسخ‌ها ثبت شد. نمره: ${result.score}/100`);setActive(null);await load()}catch(e){setErr(e instanceof Error?e.message:"خطا")}}
 return <><header><h1>پرتال دانشجو</h1></header>{err&&<div className="error">{err}</div>}{notice&&<div className="card">{notice}</div>}
 {!active?<><section className="card"><h2>آزمون‌های من</h2>{es.length===0?<p className="muted">آزمون منتشرشده‌ای وجود ندارد.</p>:es.map(e=><article className="notice" key={e.id}><b>{e.title}</b><p>مدت: {e.durationMinutes} دقیقه | تعداد سؤال: {e.questionCount} | {e.attempted?"انجام شده":"آماده"}</p>{!e.attempted&&<button onClick={()=>begin(e.id)}>شروع آزمون</button>}</article>)}</section><Table title="سوابق آزمون" rows={as.map(a=>({آزمون:a.examTitle,شروع:a.startedAt,ارسال:a.submittedAt??"—",نمره:a.score??"در انتظار تصحیح"}))}/></>:<section className="card"><h2>{active.title}</h2><p>مدت آزمون: {active.durationMinutes} دقیقه</p>{(active.questions??[]).map((q:any,index:number)=><article className="notice" key={q.id}><b>{index+1}. {q.text}</b><p>امتیاز: {q.points}</p>{q.type==="Essay"||q.type===1?<textarea value={answers[q.id]?.essayText??""} onChange={e=>setAnswers(a=>({...a,[q.id]:{...a[q.id],essayText:e.target.value}}))} placeholder="پاسخ تشریحی"/>:(q.options??[]).map((o:any)=><label key={o.id} style={{display:"block",margin:"8px 0"}}><input type="radio" name={q.id} checked={answers[q.id]?.selectedOptionId===o.id} onChange={()=>setAnswers(a=>({...a,[q.id]:{selectedOptionId:o.id}}))}/>{o.text}</label>)}</article>)}<button onClick={submit}>ثبت نهایی پاسخ‌نامه</button><button onClick={()=>setActive(null)}>بازگشت</button></section>}
 <section className="card"><h2>اعلان‌ها</h2>{ns.map(n=><article className={n.isRead?"notice read":"notice"} key={n.id}><b>{n.title}</b><p>{n.message}</p>{!n.isRead&&<button onClick={async()=>{await markNotificationRead(n.id);load()}}>خوانده شد</button>}</article>)}</section></>;
}
function Management({role}:{role:Role}){
 const [tab,setTab]=useState("students"),[rows,setRows]=useState<any[]>([]),[error,setError]=useState(""),[notice,setNotice]=useState(""),[form,setForm]=useState<Record<string,string>>({});
 const [me,setMe]=useState<any>({}),[deps,setDeps]=useState<Department[]>([]),[teacherRows,setTeacherRows]=useState<Teacher[]>([]);
 const tabs=role==="SuperAdmin"?["students","teachers","departments","department-managers","courses","exams","answer-keys"]:role==="Department"?["students","teachers","courses","exams"]:role==="Teacher"?["students","exams","answer-keys"]:[];
 useEffect(()=>{if(!tabs.includes(tab))setTab(tabs[0]??"students")},[role]);
 async function load(){
  setError("");
  try{
   const path=tab==="students"?"/students":tab==="teachers"?"/teachers":tab==="departments"?"/departments":tab==="courses"?"/courses":tab==="exams"?"/exams":tab==="answer-keys"?"/answer-keys":"/departments";
   const data=await get<any[]>(path);setRows(data);
  }catch(e){setRows([]);setError(e instanceof Error?e.message:"خطا")}
 }
 useEffect(()=>{get<any>("/portal/me").then(setMe).catch(()=>{});departments().then(setDeps).catch(()=>{});teachers().then(setTeacherRows).catch(()=>{});},[]);
 useEffect(()=>{load();setForm({})},[tab]);
 const scopeId=me.departmentId??"";
 function set(k:string,v:string){setForm(f=>({...f,[k]:v}))}
 const field=(key:string,label:string,type="text",required=true)=><label key={key}>{label}<input type={type} value={form[key]??""} onChange={e=>set(key,e.target.value)} required={required}/></label>;
 const departmentField=role==="SuperAdmin"?<label>دپارتمان<select value={form.departmentId??""} onChange={e=>set("departmentId",e.target.value)} required><option value="">انتخاب دپارتمان</option>{deps.map(d=><option value={d.id} key={d.id}>{d.name}</option>)}</select></label>:null;
 async function create(e:FormEvent){
  e.preventDefault();setError("");setNotice("");
  try{
   let path="",body:any={};
   const departmentId=form.departmentId||scopeId;
   if(tab==="students"){path="/students";body={fullName:form.fullName,username:form.username,email:form.email||"",mobile:form.mobile||"",studentNumber:form.studentNumber,entryYear:form.entryYear,departmentId,gpa:form.gpa?Number(form.gpa):null,status:"Active"}}
   else if(tab==="teachers"){path="/teachers";body={fullName:form.fullName,username:form.username,email:form.email||"",mobile:form.mobile||"",password:"",departmentId,specialty:form.specialty,status:"Active"}}
   else if(tab==="departments"){path="/departments";body={name:form.name,managerName:form.managerName||null,degreeLevel:form.degreeLevel}}
   else if(tab==="department-managers"){path="/department-managers";body={fullName:form.fullName,username:form.username,email:form.email||"",mobile:form.mobile||"",password:"",departmentId:form.departmentId}}
   else if(tab==="courses"){path="/courses";body={departmentId,name:form.name,code:form.code,description:form.description||null,isActive:true}}
   else if(tab==="exams"){path="/exams";body={departmentId,teacherId:form.teacherId||(me.teacherId??""),title:form.title,durationMinutes:Number(form.durationMinutes),passingScore:Number(form.passingScore||0),scheduledAt:new Date(form.scheduledAt).toISOString(),allowNavigation:form.allowNavigation==="true",questionIds:(form.questionIds||"").split(",").map(x=>x.trim()).filter(Boolean)}}
   else if(tab==="answer-keys"){path="/answer-keys";let options:any[]=[];try{options=JSON.parse(form.options||"[]")}catch{throw new Error("گزینه‌ها باید JSON معتبر باشند.")};body={departmentId,teacherId:form.teacherId||(me.teacherId??""),text:form.text,type:form.type==="Essay"?1:0,points:Number(form.points||1),options}}
   const result=await post<any>(path,body);
   const temporary=result?.temporaryPassword??result?.temporary_password;
   setNotice(temporary?`حساب ساخته شد. رمز موقت یک‌بار نمایش داده می‌شود: ${temporary}`:"عملیات با موفقیت انجام شد.");
   setForm({});await load();
  }catch(e){setError(e instanceof Error?e.message:"خطا")}
 }
 const canCreate=["students","teachers","departments","department-managers","courses","exams","answer-keys"].includes(tab);
 return <><header><h1>مدیریت</h1><span>{role}</span></header><nav className="tabs">{tabs.map(x=><button className={tab===x?"active":""} onClick={()=>setTab(x)} key={x}>{x}</button>)}</nav>{error&&<div className="error">{error}</div>}{notice&&<div className="card">{notice}</div>}
 {canCreate&&<form className="card" onSubmit={create}><h2>افزودن {tab}</h2>
 {["students","teachers","department-managers"].includes(tab)&&<>{field("fullName","نام کامل")}{field("username","نام کاربری")}{field("email","ایمیل","email",false)}{field("mobile","موبایل","tel",false)}</>}
 {tab==="students"&&<>{field("studentNumber","شماره دانشجویی")}{field("entryYear","سال ورود")}{field("gpa","معدل","number",false)}{departmentField}</>}
 {tab==="teachers"&&<>{field("specialty","تخصص")}{departmentField}</>}
 {tab==="departments"&&<>{field("name","نام دپارتمان")}{field("managerName","نام مدیر","text",false)}{field("degreeLevel","مقطع")}</>}
 {tab==="department-managers"&&departmentField}
 {tab==="courses"&&<>{field("name","نام درس")}{field("code","کد درس")}{field("description","توضیحات","text",false)}{departmentField}</>}
 {tab==="exams"&&<>{field("title","عنوان آزمون")}{field("durationMinutes","مدت (دقیقه)","number")}{field("passingScore","حدنصاب","number",false)}{field("scheduledAt","زمان شروع","datetime-local")}{field("teacherId","شناسه استاد (Teacher Id)", "text",role!=="Teacher")}{field("questionIds","شناسه سؤال‌ها با کاما جدا شود","text",false)}{departmentField}<label>اجازه جابه‌جایی سؤال‌ها<select value={form.allowNavigation??"true"} onChange={e=>set("allowNavigation",e.target.value)}><option value="true">بله</option><option value="false">خیر</option></select></label></>}
 {tab==="answer-keys"&&<>{field("text","متن سؤال")}<label>نوع سؤال<select value={form.type??"MultipleChoice"} onChange={e=>set("type",e.target.value)}><option>MultipleChoice</option><option>Essay</option></select></label>{field("points","امتیاز","number")}{field("teacherId","شناسه استاد (Teacher Id)","text",role!=="Teacher")}{departmentField}<label>گزینه‌ها به‌صورت JSON<textarea value={form.options??"[]" } onChange={e=>set("options",e.target.value)} placeholder='[{"text":"گزینه اول","isCorrect":true},{"text":"گزینه دوم","isCorrect":false}]'/></label></>}
 <button type="submit">ثبت</button></form>}
 <Table title={tab} rows={rows.map(x=>Object.fromEntries(Object.entries(x).filter(([k])=>!["options"].includes(k)).slice(0,9)))}/>
 </>;
}
function App(){
 const [user,setUser]=useState<LoginResponse|null>(()=>{try{return JSON.parse(localStorage.getItem("mft_user")||"null")}catch{return null}});
 const [page,setPage]=useState("dashboard");const role=user?.role;
 const logout=()=>{localStorage.removeItem("mft_token");localStorage.removeItem("mft_user");setUser(null)};
 if(!user)return <Login onLogin={x=>{localStorage.setItem("mft_token",x.token);localStorage.setItem("mft_user",JSON.stringify(x));setUser(x)}}/>;
 if(user.mustChangePassword)return <ChangePassword onDone={()=>{const u={...user,mustChangePassword:false};localStorage.setItem("mft_user",JSON.stringify(u));setUser(u)}}/>;
 const isStudent=role==="Student";const nav=isStudent?["dashboard","portal"]:["dashboard","management"];
 return <div className="shell"><aside><h2>MFT</h2>{nav.map(x=><button className={page===x?"active":""} onClick={()=>setPage(x)} key={x}>{x==="dashboard"?"داشبورد":isStudent?"پرتال دانشجو":"مدیریت"}</button>)}<button onClick={logout}>خروج</button></aside><main className="content">{page==="dashboard"?<Dashboard role={role!}/>:isStudent?<StudentPortal/>:<Management role={role!}/>}</main></div>;
}
export default App;
