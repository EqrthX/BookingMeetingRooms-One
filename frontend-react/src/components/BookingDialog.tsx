import { useEffect, useRef, useState } from 'react'
import type { FormEvent } from 'react'
import type { BookingDraft, Room } from '../types'

type Props = {
  initial: BookingDraft
  rooms: Room[]
  editing: boolean
  onClose: () => void
  onSubmit: (draft: BookingDraft) => void | Promise<void>
  submitting: boolean
  serverError: string
}

export default function BookingDialog({ initial, rooms, editing, onClose, onSubmit, submitting, serverError }: Props) {
  const ref = useRef<HTMLDialogElement>(null)
  const [draft, setDraft] = useState(initial)
  const [error, setError] = useState('')

  useEffect(() => { ref.current?.showModal() }, [])

  function submit(event: FormEvent) {
    event.preventDefault()
    if (submitting) return
    if (!draft.title.trim()) { setError('กรุณากรอกหัวข้อการประชุม'); return }
    if (new Date((draft.endDate || draft.date) + "T" + draft.end) <= new Date(draft.date + "T" + draft.start)) { setError('เวลาสิ้นสุดต้องมากกว่าเวลาเริ่มต้น'); return }
    onSubmit({ ...draft, title: draft.title.trim() })
  }

  return (
    <dialog ref={ref} className="booking-dialog" onCancel={event => { if (submitting) event.preventDefault(); else onClose() }}>
      <h2>{editing ? 'แก้ไขการจอง' : 'เพิ่มการจอง'}</h2>
      <form onSubmit={submit}>
        <fieldset disabled={submitting}>
        <label htmlFor="booking-title">หัวข้อการประชุม</label>
        <input id="booking-title" value={draft.title} onChange={e => setDraft({ ...draft, title: e.target.value })} required maxLength={120} autoFocus />
        <label htmlFor="booking-room">ห้องประชุม</label>
        <select id="booking-room" value={draft.roomId} onChange={e => setDraft({ ...draft, roomId: Number(e.target.value) })}>
          {rooms.map(room => <option key={room.Id} value={room.Id}>{room.Name} ({room.Capacity} คน)</option>)}
        </select>
        <label htmlFor="booking-date">วันที่</label>
        <input id="booking-date" type="date" value={draft.date} onChange={e => setDraft({ ...draft, date: e.target.value, endDate: e.target.value })} required />
        <div className="form-columns">
          <div><label htmlFor="booking-start">เวลาเริ่ม</label><input id="booking-start" type="time" value={draft.start} onChange={e => setDraft({ ...draft, start: e.target.value })} required /></div>
          <div><label htmlFor="booking-end">เวลาสิ้นสุด</label><input id="booking-end" type="time" value={draft.end} onChange={e => setDraft({ ...draft, end: e.target.value })} required /></div>
        </div>
        {draft.endDate && draft.endDate !== draft.date && <p>วันที่สิ้นสุด: {draft.endDate}</p>}
        {serverError && <p className="form-error" role="alert">{serverError}</p>}
        {error && <p className="form-error" role="alert">{error}</p>}
        <div className="dialog-actions">
          <button className="button" type="button" onClick={onClose}>ยกเลิก</button>
          <button className="button primary" type="submit">{submitting ? "กำลังบันทึก..." : "บันทึก"}</button>
        </div>
        </fieldset>
      </form>
    </dialog>
  )
}
