"use client"

import type React from "react"
import { useState, FormEvent } from "react"
import { Button } from "@/components/ui/button"
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card"
import { Input } from "@/components/ui/input"
import { Label } from "@/components/ui/label"
import { Tabs, TabsContent, TabsList, TabsTrigger } from "@/components/ui/tabs"
import { ThemeToggle } from "@/components/theme-toggle"
import { Cloud, Mail, Lock, User, Shield } from "lucide-react"
import Link from "next/link"
import { login, register } from "@/lib/api"
import { useRouter } from "next/navigation"

export default function AuthPage() {
  const [isLoading, setIsLoading] = useState(false)
  const [formData, setFormData] = useState({
    loginEmail: "",
    loginPassword: "",
    registerUsername: "",
    registerEmail: "",
    registerPassword: "",
    registerConfirmPassword: "",
  })
  const router = useRouter()

  const handleInputChange = (e: React.ChangeEvent<HTMLInputElement>) => {
    const { id, value } = e.target
    setFormData(prev => ({
      ...prev,
      [id]: value
    }))
  }

  const handleLogin = async (e: FormEvent) => {
    e.preventDefault()
    setIsLoading(true)
    
    try {
      const response = await login({
        email: formData.loginEmail,
        password: formData.loginPassword,
      })
      
      // Сохраняем токен в localStorage
      localStorage.setItem("token", response.token)
      
      // Перенаправляем на главную страницу
      router.push("/dashboard")
    } catch (error) {
      console.error("Ошибка при входе:", error)
      setIsLoading(false)
    }
  }

  const handleRegister = async (e: FormEvent) => {
    e.preventDefault()
    setIsLoading(true)
    
    try {
      await register({
        username: formData.registerUsername,
        email: formData.registerEmail,
        password: formData.registerPassword,
        confirmPassword: formData.registerConfirmPassword,
      })
    } catch (error) {
      console.error("Ошибка при регистрации:", error)
      setIsLoading(false)
    }
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
            <p className="text-gray-600 dark:text-gray-400">Безопасное хранение ваших файлов</p>
            <div className="flex justify-center mt-4">
              <ThemeToggle />
            </div>
          </div>

          {/* Карточка авторизации */}
          <Card className="shadow-2xl border-0 bg-white/90 dark:bg-gray-800/90 backdrop-blur-sm">
            <Tabs defaultValue="login" className="w-full">
              <TabsList className="grid w-full grid-cols-2 mb-4">
                <TabsTrigger value="login">Вход</TabsTrigger>
                <TabsTrigger value="register">Регистрация</TabsTrigger>
              </TabsList>

              {/* Вкладка входа */}
              <TabsContent value="login">
                <CardHeader className="space-y-3 pb-2">
                  <CardTitle className="text-2xl text-center">Добро пожаловать</CardTitle>
                  <CardDescription className="text-center">Войдите в свой аккаунт Cloud Drive</CardDescription>
                </CardHeader>
                <CardContent className="space-y-4 pt-4">
                  <form onSubmit={handleLogin} className="space-y-4">
                    <div className="space-y-2">
                      <Label htmlFor="loginEmail">Логин или email</Label>
                      <div className="relative">
                        <Mail className="absolute left-3 top-3 h-4 w-4 text-gray-400" />
                        <Input 
                          id="loginEmail" 
                          type="text" 
                          placeholder="example@email.com" 
                          className="pl-10" 
                          required 
                          value={formData.loginEmail}
                          onChange={handleInputChange}
                        />
                      </div>
                    </div>
                    <div className="space-y-2">
                      <Label htmlFor="loginPassword">Пароль</Label>
                      <div className="relative">
                        <Lock className="absolute left-3 top-3 h-4 w-4 text-gray-400" />
                        <Input 
                          id="loginPassword" 
                          type="password" 
                          placeholder="••••••••" 
                          className="pl-10" 
                          required 
                          value={formData.loginPassword}
                          onChange={handleInputChange}
                        />
                      </div>
                    </div>
                    <div className="flex items-center space-x-2">
                      <input
                        id="remember"
                        type="checkbox"
                        className="h-4 w-4 text-blue-600 focus:ring-blue-500 border-gray-300 rounded"
                      />
                      <Label htmlFor="remember" className="text-sm text-gray-600 dark:text-gray-300">
                        Запомнить меня
                      </Label>
                    </div>
                    <div className="flex items-center justify-between">
                    <Link
                      href="/forgot-password"
                      className="text-sm text-blue-600 hover:text-blue-500 dark:text-blue-400"
                    >
                      Забыли пароль?
                    </Link>
                  </div>
                    <Button
                      type="submit"
                      className="w-full bg-gradient-to-r from-blue-600 to-purple-600 hover:from-blue-700 hover:to-purple-700 transition-all duration-300 text-white"
                      disabled={isLoading}
                    >
                      {isLoading ? "Вход..." : "Войти"}
                    </Button>
                  </form>
                  

                  <div className="relative my-4">
                    <div className="absolute inset-0 flex items-center">
                      <div className="w-full border-t border-gray-200 dark:border-gray-700"></div>
                    </div>
                    <div className="relative flex justify-center text-xs uppercase">
                      <span className="bg-white dark:bg-gray-800 px-2 text-gray-500 dark:text-gray-400">или</span>
                    </div>
                  </div>

                  <Button variant="outline" className="w-full" asChild>
                    <Link href="/auth-code" className="flex items-center justify-center">
                      <Shield className="mr-2 h-4 w-4" />
                      Войти с кодом аутентификации
                    </Link>
                  </Button>
                </CardContent>
              </TabsContent>

              {/* Вкладка регистрации */}
              <TabsContent value="register">
                <CardHeader className="space-y-3 pb-2">
                  <CardTitle className="text-2xl text-center">Создать аккаунт</CardTitle>
                  <CardDescription className="text-center">
                    Зарегистрируйтесь для использования Cloud Drive
                  </CardDescription>
                </CardHeader>
                <CardContent className="space-y-4 pt-4">
                  <form onSubmit={handleRegister} className="space-y-4">
                    <div className="space-y-2">
                      <Label htmlFor="registerUsername">Имя пользователя</Label>
                      <div className="relative">
                        <User className="absolute left-3 top-3 h-4 w-4 text-gray-400" />
                        <Input
                          id="registerUsername"
                          type="text"
                          placeholder="Введите логин"
                          className="pl-10"
                          required
                          value={formData.registerUsername}
                          onChange={handleInputChange}
                        />
                      </div>
                    </div>
                    <div className="space-y-2">
                      <Label htmlFor="registerEmail">Email</Label>
                      <div className="relative">
                        <Mail className="absolute left-3 top-3 h-4 w-4 text-gray-400" />
                        <Input
                          id="registerEmail"
                          type="email"
                          placeholder="example@email.com"
                          className="pl-10"
                          required
                          value={formData.registerEmail}
                          onChange={handleInputChange}
                        />
                      </div>
                    </div>
                    <div className="space-y-2">
                      <Label htmlFor="registerPassword">Пароль</Label>
                      <div className="relative">
                        <Lock className="absolute left-3 top-3 h-4 w-4 text-gray-400" />
                        <Input
                          id="registerPassword"
                          type="password"
                          placeholder="••••••••"
                          className="pl-10"
                          required
                          value={formData.registerPassword}
                          onChange={handleInputChange}
                        />
                      </div>
                    </div>
                    <div className="space-y-2">
                      <Label htmlFor="registerConfirmPassword">Подтвердите пароль</Label>
                      <div className="relative">
                        <Lock className="absolute left-3 top-3 h-4 w-4 text-gray-400" />
                        <Input
                          id="registerConfirmPassword"
                          type="password"
                          placeholder="••••••••"
                          className="pl-10"
                          required
                          value={formData.registerConfirmPassword}
                          onChange={handleInputChange}
                        />
                      </div>
                    </div>
                    <Button
                      type="submit"
                      className="w-full bg-gradient-to-r from-blue-600 to-purple-600 hover:from-blue-700 hover:to-purple-700 transition-all duration-300 text-white"
                      disabled={isLoading}
                    >
                      {isLoading ? "Создание..." : "Создать аккаунт"}
                    </Button>
                  </form>
                </CardContent>
              </TabsContent>
            </Tabs>
          </Card>

          {/* Футер */}
          <div className="text-center text-sm text-gray-500 dark:text-gray-400">
            <p>© 2025 Cloud Drive. Все права защищены.</p>
          </div>
        </div>
      </div>
    </div>
  )
}
