"use client"

import type React from "react"

import { useState } from "react"
import { Button } from "@/components/ui/button"
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card"
import { Input } from "@/components/ui/input"
import { Label } from "@/components/ui/label"
import { ThemeToggle } from "@/components/theme-toggle"
import { Cloud, Mail, ArrowLeft, Send, CheckCircle } from "lucide-react"
import Link from "next/link"

export default function ForgotPasswordPage() {
  const [isLoading, setIsLoading] = useState(false)
  const [emailSent, setEmailSent] = useState(false)

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault()
    setIsLoading(true)
    // Здесь будет интеграция с ASP.NET Core API
    setTimeout(() => {
      setIsLoading(false)
      setEmailSent(true)
    }, 2000)
  }

  return (
    <div className="min-h-screen flex flex-col bg-gradient-to-br from-purple-50 via-white to-blue-50 dark:from-gray-950 dark:via-gray-900 dark:to-gray-800">
      {/* Верхняя декоративная полоса */}
      <div className="h-2 bg-gradient-to-r from-purple-500 via-blue-500 to-cyan-500"></div>

      <div className="flex-1 flex items-center justify-center p-4">
        <div className="w-full max-w-md space-y-8">
          {/* Логотип и заголовок */}
          <div className="text-center space-y-2">
            <div className="flex items-center justify-center mb-2">
              <div className="relative">
                <div className="absolute inset-0 bg-gradient-to-r from-purple-500 to-blue-500 rounded-full blur-lg opacity-70"></div>
                <div className="relative bg-white dark:bg-gray-900 rounded-full p-3 shadow-xl">
                  <Cloud className="h-10 w-10 text-blue-600 dark:text-blue-400" />
                </div>
              </div>
            </div>
            <h1 className="text-3xl font-bold text-gray-900 dark:text-white">Cloud Drive</h1>
            <p className="text-gray-600 dark:text-gray-400">Восстановление доступа</p>
            <div className="flex justify-center mt-4">
              <ThemeToggle />
            </div>
          </div>

          {/* Карточка восстановления пароля */}
          <Card className="shadow-2xl border-0 bg-white/90 dark:bg-gray-800/90 backdrop-blur-sm">
            <CardHeader className="space-y-1">
              <CardTitle className="text-2xl text-center">
                {emailSent ? "Письмо отправлено" : "Восстановление пароля"}
              </CardTitle>
              <CardDescription className="text-center">
                {emailSent
                  ? "Проверьте свою почту для дальнейших инструкций"
                  : "Введите ваш email для восстановления пароля"}
              </CardDescription>
            </CardHeader>
            <CardContent className="space-y-6">
              {!emailSent ? (
                <form onSubmit={handleSubmit} className="space-y-4">
                  <div className="space-y-2">
                    <Label htmlFor="email">Логин или email</Label>
                    <div className="relative">
                      <Mail className="absolute left-3 top-3 h-4 w-4 text-gray-400" />
                      <Input id="email" type="text" placeholder="example@email.com" className="pl-10" required />
                    </div>
                  </div>
                  <Button
                    type="submit"
                    className="w-full bg-gradient-to-r from-blue-600 to-purple-600 hover:from-blue-700 hover:to-purple-700 transition-all duration-300 text-white"
                    disabled={isLoading}
                  >
                    {isLoading ? "Отправка..." : "Отправить код подтверждения"}
                  </Button>
                </form>
              ) : (
                <div className="text-center space-y-6">
                  <div className="flex justify-center">
                    <div className="relative">
                      <div className="absolute inset-0 bg-green-500 rounded-full blur-lg opacity-20"></div>
                      <div className="relative bg-green-100 dark:bg-green-900/30 rounded-full p-4">
                        <CheckCircle className="h-12 w-12 text-green-600 dark:text-green-400" />
                      </div>
                    </div>
                  </div>
                  <div className="space-y-2">
                    <p className="text-gray-600 dark:text-gray-300">
                      Мы отправили инструкции по восстановлению пароля на ваш email адрес.
                    </p>
                    <p className="text-sm text-gray-500 dark:text-gray-400">
                      Если вы не получили письмо, проверьте папку "Спам".
                    </p>
                  </div>
                  <Button
                    asChild
                    className="w-full bg-gradient-to-r from-blue-600 to-purple-600 hover:from-blue-700 hover:to-purple-700 transition-all duration-300 text-white"
                  >
                    <Link href="/">
                      Вернуться ко входу
                    </Link>
                  </Button>
                </div>
              )}

              {!emailSent && (
                <div className="text-center pt-2">
                  <Link
                    href="/"
                    className="text-sm text-blue-600 hover:text-blue-500 dark:text-blue-400 inline-flex items-center"
                  >
                    <ArrowLeft className="mr-2 h-4 w-4" />
                    Вернуться ко входу
                  </Link>
                </div>
              )}
            </CardContent>
          </Card>
        </div>
      </div>
    </div>
  )
}
