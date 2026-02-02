import { UserButton } from '@clerk/nextjs'
import { auth } from '@clerk/nextjs/server'
import Link from 'next/link'

export default async function AppPage() {
  const { userId } = await auth()

  return (
    <div className="min-h-screen bg-gray-50">
      <header className="bg-white shadow">
        <div className="max-w-7xl mx-auto px-4 py-4 sm:px-6 lg:px-8 flex justify-between items-center">
          <h1 className="text-2xl font-bold text-gray-900">BitgetLab - User Dashboard</h1>
          <div className="flex items-center gap-4">
            <Link href="/admin" className="text-blue-600 hover:text-blue-800">
              Admin
            </Link>
            <UserButton />
          </div>
        </div>
      </header>

      <main className="max-w-7xl mx-auto py-6 sm:px-6 lg:px-8">
        <div className="px-4 py-6 sm:px-0">
          <div className="bg-white shadow rounded-lg p-6">
            <h2 className="text-xl font-semibold mb-4">Welcome to BitgetLab</h2>
            <p className="text-gray-600 mb-4">User ID: {userId}</p>
            
            <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-4 mt-6">
              <div className="border rounded-lg p-4">
                <h3 className="font-semibold mb-2">Active Bots</h3>
                <p className="text-3xl font-bold text-blue-600">0</p>
              </div>
              
              <div className="border rounded-lg p-4">
                <h3 className="font-semibold mb-2">Total Trades</h3>
                <p className="text-3xl font-bold text-green-600">0</p>
              </div>
              
              <div className="border rounded-lg p-4">
                <h3 className="font-semibold mb-2">PnL</h3>
                <p className="text-3xl font-bold text-gray-600">$0.00</p>
              </div>
            </div>
          </div>
        </div>
      </main>
    </div>
  )
}
