import { Navigate, Route, Routes, useNavigate } from 'react-router-dom'
import Login from './pages/Login'
import Dashboard from './pages/Dashboard'
import './App.css'

export default function App() {
  const navigate = useNavigate()

  // Page routing only. Add authentication checks when integrating your API.
  return (
    <Routes>
      <Route path="/" element={<Navigate to="/login" replace />} />
      <Route
        path="/login"
        element={<Login />}
      />
      <Route
        path="/dashboard"
        element={<Dashboard onLogout={() => navigate('/login')} />}
      />
      <Route path="*" element={<Navigate to="/login" replace />} />
    </Routes>
  )
}
