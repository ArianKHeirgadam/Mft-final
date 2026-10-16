export type Role="Student"|"Teacher"|"Department"|"SuperAdmin";
export interface LoginResponse{token:string;userId:string;fullName:string;role:Role;mustChangePassword:boolean}
export interface Student{id:string;fullName:string;studentNumber:string;department:string;degreeLevel:string;gpa:number|null;status:string}
export interface Teacher{id:string;fullName:string;department:string;specialty:string;status:string}
export interface Department{id:string;name:string;managerName:string|null;degreeLevel:string;isActive:boolean;studentCount:number;teacherCount:number}
export interface Exam{id:string;title:string;department:string;teacher:string;durationMinutes:number;passingScore:number;scheduledAt:string;status:string;questionCount:number}
export interface Notification{id:string;title:string;message:string;isRead:boolean;createdAt:string}
