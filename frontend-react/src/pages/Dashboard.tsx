import { useEffect, useRef, useState } from 'react'
import type { Booking, BookingDraft, Room, ApiBooking, StoredUser } from '../types'
import BookingDialog from '../components/BookingDialog'
import { useNavigate } from 'react-router-dom'
import { apiRequest, ApiError } from '../api/client'

function localDate(value = new Date()) {
  return [value.getFullYear(), String(value.getMonth() + 1).padStart(2, '0'), String(value.getDate()).padStart(2, '0')].join('-')
}
function localTime(value: Date) {
  return String(value.getHours()).padStart(2, '0') + ':' + String(value.getMinutes()).padStart(2, '0')
}
function toViewBooking(item: ApiBooking): Booking {
  const start = new Date(item.startTime), end = new Date(item.endTime)
  return { id: item.id, roomId: item.roomId, userId: item.userId, title: item.title,
    date: localDate(start), start: localTime(start), end: localTime(end),
    endDate: localDate(end), owner: item.ownerName || 'ผู้ใช้ #' + item.userId, initials: '' }
}
export default function Dashboard({ onLogout }: { onLogout: () => void }) {
  const [date, setDate] = useState(localDate)
  const [roomFilter, setRoomFilter] = useState('all')
  const [query, setQuery] = useState('')
  const [mineOnly, setMineOnly] = useState(false)
  const [dialog, setDialog] = useState<{ initial: BookingDraft; id?: number } | null>(null)
  const [deleting, setDeleting] = useState<Booking | null>(null)
  const [notice, setNotice] = useState('')
  const [error, setError] = useState('')
  const [formError, setFormError] = useState('')
  const [busy, setBusy] = useState(false)
  const [loading, setLoading] = useState(true)
  const deleteRef = useRef<HTMLDialogElement>(null)
  const navigate = useNavigate()
  const [user] = useState<StoredUser | null>(() => {
    try { return JSON.parse(localStorage.getItem('user') || 'null') as StoredUser | null }
    catch { return null }
  })
  const [rooms, setRooms] = useState<Room[]>([])
  const [bookings, setBookings] = useState<Booking[]>([])

  function logout() {
    localStorage.removeItem('token')
    localStorage.removeItem('user')
    onLogout()
  }

  useEffect(() => {
    if (!localStorage.getItem('token')) { navigate('/login', { replace: true }); return }
    let active = true
    Promise.all([apiRequest<Room[]>('/Crud/rooms'), apiRequest<ApiBooking[]>('/Crud/bookings')])
      .then(([roomData, bookingData]) => {
        if (!active) return
        setRooms(roomData)
        setBookings(bookingData.map(toViewBooking))
      }).catch((failure: unknown) => {
        if (!active) return
        if (failure instanceof ApiError && failure.status === 401) {
          localStorage.removeItem('token'); localStorage.removeItem('user')
          navigate('/login', { replace: true })
        } else setError(failure instanceof Error ? failure.message : 'โหลดข้อมูลไม่สำเร็จ')
      }).finally(() => { if (active) setLoading(false) })
    return () => { active = false }
  }, [navigate])
  useEffect(() => { if (deleting) deleteRef.current?.showModal() }, [deleting])

  const filtered = bookings.filter(booking =>
    booking.date === date && (roomFilter === 'all' || booking.roomId === Number(roomFilter)) &&
    (!mineOnly || booking.userId === user?.id) &&
    booking.title.toLowerCase().includes(query.toLowerCase()))

  function openBooking(roomId = rooms[0]?.Id, booking?: Booking) {
    if (!roomId) return
    setFormError('')
    setDialog({
      initial: booking
        ? { roomId: booking.roomId, title: booking.title, date: booking.date, endDate: booking.endDate, start: booking.start, end: booking.end }
        : { roomId, title: '', date, start: '09:00', end: '10:00' },
      id: booking?.id,
    })
  }
  async function refreshBookings() {
    setBookings((await apiRequest<ApiBooking[]>('/Crud/bookings')).map(toViewBooking))
  }
  function failureMessage(failure: unknown) {
    if (failure instanceof ApiError && failure.status === 401) { logout(); return 'กรุณาเข้าสู่ระบบใหม่' }
    if (failure instanceof ApiError && failure.status === 409)
      return 'ห้องนี้ไม่ว่างในช่วงเวลาที่เลือก กรุณาเลือกห้องหรือเวลาใหม่'
    return failure instanceof Error ? failure.message : 'ดำเนินการไม่สำเร็จ'
  }
  async function saveBooking(draft: BookingDraft) {
    if (busy || !dialog) return
    setBusy(true); setFormError(''); setError('')
    const editingId = dialog.id
    try {
      const startTime = new Date(draft.date + 'T' + draft.start).toISOString()
      const endTime = new Date((draft.endDate || draft.date) + 'T' + draft.end).toISOString()
      const saved = await apiRequest<ApiBooking>(
        editingId === undefined ? '/Crud/booking' : '/Crud/bookings/' + editingId,
        { method: editingId === undefined ? 'POST' : 'PUT',
          body: JSON.stringify({ roomId: draft.roomId, title: draft.title, startTime, endTime }) })
      setBookings(current => [...current.filter(item => item.id !== saved.id), toViewBooking(saved)])
      setDate(draft.date); setDialog(null)
      setNotice(editingId === undefined ? 'บันทึกการจองสำเร็จ' : 'แก้ไขการจองสำเร็จ')
      try { await refreshBookings() }
      catch { setError('บันทึกสำเร็จแล้ว แต่โหลดรายการล่าสุดไม่สำเร็จ กรุณารีเฟรชหน้า') }
    } catch (failure) { setFormError(failureMessage(failure)) }
    finally { setBusy(false) }
  }
  async function deleteBooking() {
    if (busy || !deleting) return
    setBusy(true); setError('')
    try {
      await apiRequest<void>('/Crud/bookings/' + deleting.id, { method: 'DELETE' })
      setBookings(current => current.filter(item => item.id !== deleting.id))
      setDeleting(null); setNotice('ลบการจองสำเร็จ')
      try { await refreshBookings() }
      catch { setError('ลบสำเร็จแล้ว แต่โหลดรายการล่าสุดไม่สำเร็จ กรุณารีเฟรชหน้า') }
    } catch (failure) { setError(failureMessage(failure)); setDeleting(null) }
    finally { setBusy(false) }
  }

  return (
    <div>
      <header className="header">
        <strong>ระบบจองห้องประชุม</strong>
        <p>คุณ {user?.firstName} {user?.lastName}</p>
        <button className="button" onClick={logout}>ออกจากระบบ</button>
      </header>

      <main className="dashboard">
        <div className="section-heading">
          <h1>จองห้องประชุม</h1>
          <button className="button primary" disabled={loading || rooms.length === 0 || busy} onClick={() => openBooking()}>เพิ่มการจอง</button>
        </div>

        {loading && <p>กำลังโหลดข้อมูล...</p>}
        {error && <p className="form-error" role="alert">{error}</p>}
        {notice && (
          <div className="notice" role="status">
            <span>{notice}</span>
            <button className="text-button" onClick={() => setNotice('')}>ปิด</button>
          </div>
        )}

        <section className="panel">
          <h2>ห้องประชุม</h2>
          <div className="room-list">
            {rooms.map(room => (
              <div className="room-item" key={room.Id}>
                <div><strong>{room.Name}</strong><p className="muted">รองรับ {room.Capacity} คน</p></div>
                <button className="button" onClick={() => openBooking(room.Id)}>จองห้องนี้</button>
              </div>
            ))}
          </div>
        </section>

        <section className="panel">
          <h2>รายการจอง</h2>
          <div className="filters">
            <label>วันที่
              <input type="date" value={date} onChange={e => { if (e.target.value) setDate(e.target.value) }} />
            </label>
            <label>ห้องประชุม
              <select value={roomFilter} onChange={e => setRoomFilter(e.target.value)}>
                <option value="all">ทั้งหมด</option>
                {rooms.map(room => <option key={room.Id} value={room.Id}>{room.Name}</option>)}
              </select>
            </label>
            <label>ค้นหาหัวข้อ
              <input placeholder="ชื่อการประชุม" value={query} onChange={e => setQuery(e.target.value)} />
            </label>
          </div>
          <label className="checkbox-label">
            <input type="checkbox" checked={mineOnly} onChange={e => setMineOnly(e.target.checked)} />
            แสดงเฉพาะการจองของฉัน
          </label>
          <div className="table-scroll">
            <table>
              <thead><tr><th>หัวข้อ</th><th>ห้องประชุม</th><th>เวลา</th><th>ผู้จอง</th><th>จัดการ</th></tr></thead>
              <tbody>
                {filtered.map(booking => (
                  <tr key={booking.id}>
                    <td>{booking.title}</td>
                    <td>{rooms.find(room => room.Id === booking.roomId)?.Name}</td>
                    <td className="nowrap">{booking.start} - {booking.end}</td>
                    <td>{booking.owner}</td>
                    <td>
                      {booking.userId === user?.id ? (
                        <div className="row-actions">
                          <button className="button" onClick={() => openBooking(booking.roomId, booking)}>แก้ไข</button>
                          <button className="button danger" onClick={() => setDeleting(booking)}>ลบ</button>
                        </div>
                      ) : '-'}
                    </td>
                  </tr>
                ))}
                {filtered.length === 0 && <tr><td colSpan={5} className="empty-state">ไม่พบรายการจอง</td></tr>}
              </tbody>
            </table>
          </div>
          <p className="muted table-count">ทั้งหมด {filtered.length} รายการ</p>
        </section>
      </main>

      {dialog && (
        <BookingDialog initial={dialog.initial} rooms={rooms} editing={dialog.id !== undefined}
          onClose={() => { if (!busy) setDialog(null) }} onSubmit={saveBooking} submitting={busy} serverError={formError} />
      )}
      {deleting && (
        <dialog ref={deleteRef} className="booking-dialog" onCancel={event => { if (busy) event.preventDefault(); else setDeleting(null) }}>
          <h2>ยืนยันการลบ</h2>
          <p>ต้องการลบการจอง “{deleting.title}” หรือไม่?</p>
          <div className="dialog-actions">
            <button className="button" disabled={busy} onClick={() => setDeleting(null)}>ยกเลิก</button>
            <button className="button danger" disabled={busy} onClick={deleteBooking}>ยืนยันลบ</button>
          </div>
        </dialog>
      )}
    </div>
  )
}
