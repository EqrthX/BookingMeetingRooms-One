import { useState, type FormEvent } from 'react'
import { useNavigate } from 'react-router-dom'
import { API_BASE } from '../api/client'

export default function Login() {
  const [showPassword, setShowPassword] = useState(false)
  const [username, setUsername] = useState('')
  const [password, setPassword] = useState('')
  const navigate = useNavigate()
  const [error, setError] = useState('')
  
  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    const form = event.target;
    const formData = new FormData(form as HTMLFormElement);

    setUsername(formData.get('username') as string);
    setPassword(formData.get('password') as string);

    const apiLogin = await fetch(API_BASE + '/Auth/login', {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json',
      },
      body: JSON.stringify({ username, password }),
    })

    if(apiLogin.ok) {
      const data = await apiLogin.json();
      localStorage.setItem('token', data.token);
      localStorage.setItem('user', JSON.stringify(data.user));
      setError('');
      window.alert('เข้าสู่ระบบสำเร็จ');
      navigate('/dashboard');
    } else {
      const errorMessage = await apiLogin.text();
      setError(errorMessage);
      console.error('Login failed:', errorMessage);
    }
  }

  return (
    <main className="login-page">
      <section className="login-box">
        <h1>ระบบจองห้องประชุม</h1>
        <p className="muted">เข้าสู่ระบบ</p>
        <form onSubmit={handleSubmit}>
          <label htmlFor="username">ชื่อผู้ใช้</label>
          <input id="username" name="username" autoComplete="username" value={username} onChange={e => setUsername(e.target.value)} required />
          <label htmlFor="password">รหัสผ่าน</label>
          <input id="password" name="password" type={showPassword ? 'text' : 'password'} autoComplete="current-password" value={password} onChange={e => setPassword(e.target.value)} required />
          <label className="checkbox-label">
            <input type="checkbox" checked={showPassword} onChange={e => setShowPassword(e.target.checked)} />
            แสดงรหัสผ่าน
          </label>
          {error && <p className="error">{error}</p>}
          <button className="button primary full" type="submit">เข้าสู่ระบบ</button>
        </form>
      </section>
    </main>
  )
}
