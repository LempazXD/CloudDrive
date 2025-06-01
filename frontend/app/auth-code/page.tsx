"use client"

import type React from "react"

import { useState, useEffect } from "react"
import { Button } from "@/components/ui/button"
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card"
import { Input } from "@/components/ui/input"
import { Label } from "@/components/ui/label"
import { ThemeToggle } from "@/components/theme-toggle"
import { Cloud, Mail, ArrowLeft, Shield, Send } from "lucide-react"
import Link from "next/link"

export default function AuthCodePage() {
  const [step, setStep] = useState<"email" | "code">("email")
  const [isLoading, setIsLoading] = useState(false)
  const [email, setEmail] = useState("")
  const [code, setCode] = useState(["", "", "", "", "", ""])
  const [activeInput, setActiveInput] = useState(0)

  const handleEmailSubmit = async (e: React.FormEvent) => {
    e.preventDefault()
    setIsLoading(true)
    // Здесь будет интеграция с ASP.NET Core API для отправки кода
    setTimeout(() => {
      setIsLoading(false)
      setStep("code")
    }, 2000)
  }

  const handleCodeSubmit = async (e: React.FormEvent) => {
    e.preventDefault()
    setIsLoading(true)
    // Здесь будет интеграция с ASP.NET Core API для проверки кода
    setTimeout(() => {
      setIsLoading(false)
      // Перенаправление в приложение
    }, 2000)
  }

  const handleCodeChange = (index: number, value: string) => {
    if (value.length > 1) {
      value = value.slice(0, 1)
    }

    const newCode = [...code]
    newCode[index] = value
    setCode(newCode)

    // Автоматический переход к следующему полю
    if (value && index < 5) {
      setActiveInput(index + 1)
    }
  }

  useEffect(() => {
    // Фокус на активном поле ввода
    if (step === "code") {
      const inputElement = document.getElementById(`code-${activeInput}`)
      if (inputElement) {
        inputElement.focus()
      }
    }
  }, [activeInput, step])

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
            <p className="text-gray-600 dark:text-gray-400">Безопасная аутентификация</p>
            <div className="flex justify-center mt-4">
              <ThemeToggle />
            </div>
          </div>

          {/* Карточка аутентификации по коду */}
          <Card className="shadow-2xl border-0 bg-white/90 dark:bg-gray-800/90 backdrop-blur-sm">
            <CardHeader className="space-y-1">
              <div className="flex items-center justify-center mb-2">
                <div className="relative">
                  <div className="absolute inset-0 bg-blue-500 rounded-full blur-md opacity-20"></div>
                  <div className="relative bg-blue-100 dark:bg-blue-900/30 rounded-full p-2">
                    <Shield className="h-6 w-6 text-blue-600 dark:text-blue-400" />
                  </div>
                </div>
              </div>
              <CardTitle className="text-2xl text-center">
                {step === "email" ? "Вход по коду" : "Введите код"}
              </CardTitle>
              <CardDescription className="text-center">
                {step === "email"
                  ? "Мы отправим код аутентификации на ваш email"
                  : `Введите 6-значный код, отправленный на ${email}`}
              </CardDescription>
            </CardHeader>
            <CardContent className="space-y-6">
              {step === "email" ? (
                <form onSubmit={handleEmailSubmit} className="space-y-4">
                  <div className="space-y-2">
                    <Label htmlFor="email">Логин или email</Label>
                    <div className="relative">
                      <Mail className="absolute left-3 top-3 h-4 w-4 text-gray-400" />
                      <Input
                        id="email"
                        type="text"
                        placeholder="example@email.com"
                        className="pl-10"
                        value={email}
                        onChange={(e) => setEmail(e.target.value)}
                        required
                      />
                    </div>
                  </div>
                  <Button
                    type="submit"
                    className="w-full bg-gradient-to-r from-blue-600 to-purple-600 hover:from-blue-700 hover:to-purple-700 transition-all duration-300"
                    disabled={isLoading}
                  >
                    {isLoading ? "Отправка кода..." : "Отправить код"}
                  </Button>
                </form>
              ) : (
                <form onSubmit={handleCodeSubmit} className="space-y-6">
                  <div className="space-y-4">
                    <Label htmlFor="code-0">Код аутентификации</Label>
                    <div className="flex justify-between gap-2">
                      {code.map((digit, index) => (
                        <Input
                          key={index}
                          id={`code-${index}`}
                          type="text"
                          inputMode="numeric"
                          pattern="[0-9]*"
                          maxLength={1}
                          className="w-12 h-12 text-center text-xl font-bold"
                          value={digit}
                          onChange={(e) => handleCodeChange(index, e.target.value)}
                          onKeyDown={(e) => {
                            if (e.key === "Backspace" && !digit && index > 0) {
                              setActiveInput(index - 1)
                            }
                          }}
                          required
                        />
                      ))}
                    </div>
                  </div>
                  <div className="text-center">
                    <button
                      type="button"
                      onClick={() => setStep("email")}
                      className="text-sm text-blue-600 hover:text-blue-500 dark:text-blue-400"
                    >
                      Не получили код? Отправить повторно
                    </button>
                  </div>
                  <Button
                    type="submit"
                    className="w-full bg-gradient-to-r from-blue-600 to-purple-600 hover:from-blue-700 hover:to-purple-700 transition-all duration-300"
                    disabled={isLoading || code.some((digit) => !digit)}
                  >
                    {isLoading ? "Проверка..." : "Войти"}
                  </Button>
                </form>
              )}

              <div className="text-center pt-2">
                <Link
                  href="/"
                  className="text-sm text-blue-600 hover:text-blue-500 dark:text-blue-400 inline-flex items-center"
                >
                  <ArrowLeft className="mr-2 h-4 w-4" />
                  Вернуться к обычному входу
                </Link>
              </div>
            </CardContent>
          </Card>
        </div>
      </div>
    </div>
  )
}
