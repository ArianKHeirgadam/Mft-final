import {get,post} from "./client"; import type {LoginResponse} from "../types";
export const login=(username:string,password:string)=>post<LoginResponse>("/auth/login",{username,password});
export const me=()=>get<Record<string,unknown>>("/portal/me");
export const changePassword=(currentPassword:string,newPassword:string)=>post<{message:string}>("/auth/change-password",{currentPassword,newPassword});
