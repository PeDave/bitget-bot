import { UserButton } from '@clerk/nextjs'
import { auth } from '@clerk/nextjs/server'
import Link from 'next/link'

export default async function AdminPage() {
  const { userId } = await auth()

  // TODO: Add role-based authorization check here
  // For now, any authenticated user can access

  return (
    <div className="min-h-screen bg-gray-50">
      <header className="bg-white shadow">
        <div className="max-w-7xl mx-auto px-4 py-4 sm:px-6 lg:px-8 flex justify-between items-center">
          <h1 className="text-2xl font-bold text-gray-900">BitgetLab - Admin Panel</h1>
          <div className="flex items-center gap-4">
            <Link href="/app" className="text-blue-600 hover:text-blue-800">
              User Dashboard
            </Link>
            <UserButton />
          </div>
        </div>
      </header>

      <main className="max-w-7xl mx-auto py-6 sm:px-6 lg:px-8">
        <div className="px-4 py-6 sm:px-0">
          <div className="bg-white shadow rounded-lg p-6 mb-6">
            <h2 className="text-xl font-semibold mb-4">System Overview</h2>
            <p className="text-gray-600 mb-4">Admin ID: {userId}</p>
            
            <div className="grid grid-cols-1 md:grid-cols-3 gap-4 mt-6">
              <div className="border rounded-lg p-4">
                <h3 className="font-semibold mb-2">Total Users</h3>
                <p className="text-3xl font-bold text-blue-600">0</p>
              </div>
              
              <div className="border rounded-lg p-4">
                <h3 className="font-semibold mb-2">Active Bots</h3>
                <p className="text-3xl font-bold text-green-600">0</p>
              </div>
              
              <div className="border rounded-lg p-4">
                <h3 className="font-semibold mb-2">System Status</h3>
                <p className="text-lg font-bold text-green-600">Healthy</p>
              </div>
            </div>
          </div>

          <div className="bg-white shadow rounded-lg p-6">
            <h2 className="text-xl font-semibold mb-4">System Services</h2>
            <div className="space-y-2">
              <div className="flex justify-between items-center p-3 border rounded">
                <span className="font-medium">API Server</span>
                <span className="text-green-600">Running</span>
              </div>
              <div className="flex justify-between items-center p-3 border rounded">
                <span className="font-medium">Worker Service</span>
                <span className="text-green-600">Running</span>
              </div>
              <div className="flex justify-between items-center p-3 border rounded">
                <span className="font-medium">Database</span>
                <span className="text-green-600">Connected</span>
              </div>
            </div>
          </div>
        </div>
      </main>
    </div>
  )
}
