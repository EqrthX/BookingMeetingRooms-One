import type { Booking } from '../types'
export const demoDate = '2026-10-04'

export const bookings: Booking[] = [
  { id: 1, roomId: 1, title: 'วางแผนงานประจำสัปดาห์', date: demoDate, start: '09:00', end: '10:00', owner: 'คุณ', initials: 'ME' },
  { id: 2, roomId: 2, title: 'Product design review', date: demoDate, start: '10:30', end: '12:00', owner: 'ทีมออกแบบ', initials: 'DS' },
  { id: 3, roomId: 3, title: 'สัมภาษณ์ผู้สมัคร', date: demoDate, start: '13:00', end: '14:00', owner: 'ทีมบุคคล', initials: 'HR' },
  { id: 4, roomId: 2, title: 'Sprint planning', date: demoDate, start: '14:30', end: '16:00', owner: 'คุณ', initials: 'ME' },
]
