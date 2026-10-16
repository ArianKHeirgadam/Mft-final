import {get,post} from "./client"; import type {Exam,Notification} from "../types";
export const myExams=()=>get<Exam[]>("/portal/my-exams");
export const startExam=(id:string)=>post<any>(`/portal/exams/${id}/start`,{});
export const myAttempts=()=>get<any[]>("/portal/my-attempts");
export const notifications=()=>get<Notification[]>("/portal/notifications");
export const markNotificationRead=(id:string)=>post<void>(`/portal/notifications/${id}/read`,{});
