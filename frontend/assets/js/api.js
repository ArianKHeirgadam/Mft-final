/* Legacy MFT API bridge. Authentication is username/password only; role is returned by the server. */
const API_BASE = window.MFT_API_BASE || 'http://localhost:51757/api';
const api = {
 async request(path, options={}) {
  const token=localStorage.getItem('mft_token');
  const headers={'Content-Type':'application/json',...(options.headers||{})};
  if(token) headers.Authorization=`Bearer ${token}`;
  const r=await fetch(API_BASE+path,{...options,headers});
  if(!r.ok){let m={};try{m=await r.json()}catch{};if(r.status===401){localStorage.removeItem('mft_token');localStorage.removeItem('mft_user');}throw new Error(m.message||`API ${r.status}`)}
  return r.status===204?null:r.json();
 },
 get(p){return this.request(p)},
 post(p,b){return this.request(p,{method:'POST',body:JSON.stringify(b)})},
 del(p){return this.request(p,{method:'DELETE'})}
};
window.MftApi=api;
function authRequired(){ if(!localStorage.getItem('mft_token') && !location.pathname.endsWith('login.html') && !location.pathname.endsWith('index.html')) location.href='login.html'; }
async function initLogin(){ 
 const f=document.getElementById('loginForm');if(!f)return;
 f.addEventListener('submit',async e=>{
  e.preventDefault();const btn=document.getElementById('loginBtn');btn.disabled=true;
  try{
   const username=document.getElementById('username').value.trim(),password=document.getElementById('password').value;
   const data=await api.post('/auth/login',{username,password});
   localStorage.setItem('mft_token',data.token);localStorage.setItem('mft_user',JSON.stringify(data));
   if(data.mustChangePassword){location.href='change-password.html';return;}
   const target={Student:'dashboard-student.html',Teacher:'dashboard-teacher.html',Department:'dashboard-department.html',SuperAdmin:'dashboard-superadmin.html'}[data.role]||'index.html';location.href=target;
  }catch(err){showToast(err.message,'danger')}finally{btn.disabled=false}
 })
}
async function initChangePassword(){if(!location.pathname.endsWith('change-password.html'))return;const f=document.getElementById('changePasswordForm');if(!f)return;f.addEventListener('submit',async e=>{e.preventDefault();const current=document.getElementById('currentPassword').value,newPassword=document.getElementById('newPassword').value,confirm=document.getElementById('confirmPassword').value;if(newPassword!==confirm)return showToast('رمز جدید و تکرار آن یکسان نیست','danger');try{await api.post('/auth/change-password',{currentPassword:current,newPassword});const u=JSON.parse(localStorage.getItem('mft_user')||'{}');u.mustChangePassword=false;localStorage.setItem('mft_user',JSON.stringify(u));showToast('رمز با موفقیت تغییر کرد','ok');setTimeout(()=>location.href='index.html',500)}catch(err){showToast(err.message,'danger')}})}
if(document.readyState==='loading')document.addEventListener('DOMContentLoaded',()=>{authRequired();initLogin();initChangePassword()});else{authRequired();initLogin();initChangePassword();}
