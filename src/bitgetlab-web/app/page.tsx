import { auth } from '@clerk/nextjs/server'
import { redirect } from 'next/navigation'
import Link from 'next/link'

export default async function Home() {
  const { userId } = await auth()

  if (userId) {
    redirect('/app')
  }

  return (
    <div className="flex min-h-screen items-center justify-center bg-gradient-to-br from-blue-50 to-indigo-100">
      <main className="flex flex-col items-center justify-center gap-8 p-8">
        <div className="text-center">
          <h1 className="text-6xl font-bold text-gray-900 mb-4">
            BitgetLab
          </h1>
          <p className="text-xl text-gray-600 mb-8">
            Advanced Cryptocurrency Trading Bot Platform
          </p>
        </div>

        <div className="flex gap-4">
          <Link
            href="/sign-in"
            className="px-6 py-3 bg-blue-600 text-white rounded-lg hover:bg-blue-700 transition-colors font-semibold"
          >
            Sign In
          </Link>
          <Link
            href="/sign-up"
            className="px-6 py-3 bg-white text-blue-600 border-2 border-blue-600 rounded-lg hover:bg-blue-50 transition-colors font-semibold"
          >
            Sign Up
          </Link>
        </div>

        <div className="mt-8 text-center max-w-2xl">
          <h2 className="text-2xl font-semibold mb-4">Features</h2>
          <div className="grid grid-cols-1 md:grid-cols-3 gap-4 mt-6">
            <div className="bg-white p-6 rounded-lg shadow">
              <h3 className="font-semibold mb-2">Bitget Integration</h3>
              <p className="text-sm text-gray-600">
                Direct integration with Bitget exchange
              </p>
            </div>
            <div className="bg-white p-6 rounded-lg shadow">
              <h3 className="font-semibold mb-2">Paper Trading</h3>
              <p className="text-sm text-gray-600">
                Test strategies without risking real funds
              </p>
            </div>
            <div className="bg-white p-6 rounded-lg shadow">
              <h3 className="font-semibold mb-2">Real-time Monitoring</h3>
              <p className="text-sm text-gray-600">
                Monitor your bots and system metrics
              </p>
            </div>
          </div>
        </div>
      </main>
    </div>
  )
}
