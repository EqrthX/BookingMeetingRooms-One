export type Room = { Id: number; Name: string; Capacity: number; Status: string }
export type Booking = { id: number; roomId: number; title: string; date: string; start: string; end: string; owner: string; initials: string; userId?: number; endDate?: string }
export type BookingDraft = { roomId: number; title: string; date: string; start: string; end: string; endDate?: string }

export type ApiBooking = { id: number; roomId: number; userId: number; title: string; startTime: string; endTime: string; ownerName: string }
export type StoredUser = { id: number; username: string; firstName: string; lastName: string }
