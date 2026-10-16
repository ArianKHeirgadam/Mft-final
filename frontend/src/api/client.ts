const base=import.meta.env.VITE_API_BASE_URL??"http://localhost:51757/api";
export async function request<T>(path:string,init:RequestInit={}):Promise<T>{
 const token=localStorage.getItem("mft_token");
 const headers=new Headers(init.headers); headers.set("Content-Type","application/json");
 if(token) headers.set("Authorization",`Bearer ${token}`);
 const response=await fetch(base+path,{...init,headers});
 if(response.status===401){localStorage.removeItem("mft_token");localStorage.removeItem("mft_user");window.location.href="/";}
 if(!response.ok){let message=`API ${response.status}`;try{const body=await response.json();message=body.message??message}catch{};throw new Error(message)}
 return response.status===204?undefined as T:await response.json() as T;
}
export const get=<T>(path:string)=>request<T>(path);
export const post=<T>(path:string,body:unknown)=>request<T>(path,{method:"POST",body:JSON.stringify(body)});
export const put=<T>(path:string,body:unknown)=>request<T>(path,{method:"PUT",body:JSON.stringify(body)});
export const del=<T=void>(path:string)=>request<T>(path,{method:"DELETE"});
