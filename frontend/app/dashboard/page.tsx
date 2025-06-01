"use client"

import { Button } from "@/components/ui/button"
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card"
import { ThemeToggle } from "@/components/theme-toggle"
import { Cloud, Upload, FolderPlus, Settings, LogOut, File, Folder, Search, Bell, Menu, User } from "lucide-react"
import Link from "next/link"
import { Input } from "@/components/ui/input"
import { Avatar, AvatarFallback, AvatarImage } from "@/components/ui/avatar"
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuLabel,
  DropdownMenuSeparator,
  DropdownMenuTrigger,
} from "@/components/ui/dropdown-menu"
import { Progress } from "@/components/ui/progress"
import { useState } from "react"

export default function DashboardPage() {
  const [sidebarOpen, setSidebarOpen] = useState(true)

  return (
    <div className="min-h-screen bg-gray-50 dark:bg-gray-950 flex flex-col">
      {/* Верхняя декоративная полоса */}
      <div className="h-1 bg-gradient-to-r from-purple-500 via-blue-500 to-cyan-500"></div>

      {/* Header */}
      <header className="bg-white dark:bg-gray-900 shadow-sm border-b border-gray-200 dark:border-gray-800">
        <div className="flex items-center justify-between h-16 px-4 md:px-6">
          <div className="flex items-center">
            <Button variant="ghost" size="icon" className="md:hidden mr-2" onClick={() => setSidebarOpen(!sidebarOpen)}>
              <Menu className="h-5 w-5" />
            </Button>
            <div className="flex items-center space-x-2">
              <div className="relative">
                <div className="absolute inset-0 bg-gradient-to-r from-purple-500 to-blue-500 rounded-full blur opacity-70"></div>
                <div className="relative bg-white dark:bg-gray-900 rounded-full p-1">
                  <Cloud className="h-6 w-6 text-blue-600 dark:text-blue-400" />
                </div>
              </div>
              <h1 className="text-xl font-bold text-gray-900 dark:text-white hidden md:block">Cloud Drive</h1>
            </div>
          </div>

          <div className="flex-1 max-w-md mx-4 hidden md:block">
            <div className="relative">
              <Search className="absolute left-3 top-2.5 h-4 w-4 text-gray-400" />
              <Input
                placeholder="Поиск файлов..."
                className="pl-10 bg-gray-50 dark:bg-gray-800 border-gray-200 dark:border-gray-700"
              />
            </div>
          </div>

          <div className="flex items-center space-x-3">
            <ThemeToggle />

            <Button variant="ghost" size="icon" className="relative">
              <Bell className="h-5 w-5" />
              <span className="absolute top-1 right-1 w-2 h-2 bg-red-500 rounded-full"></span>
            </Button>

            <DropdownMenu>
              <DropdownMenuTrigger asChild>
                <Button variant="ghost" className="relative h-9 w-9 rounded-full">
                  <Avatar className="h-9 w-9">
                    <AvatarImage src="/placeholder.svg?height=36&width=36" alt="Аватар" />
                    <AvatarFallback>ИП</AvatarFallback>
                  </Avatar>
                </Button>
              </DropdownMenuTrigger>
              <DropdownMenuContent align="end">
                <DropdownMenuLabel>Мой аккаунт</DropdownMenuLabel>
                <DropdownMenuSeparator />
                <DropdownMenuItem>
                  <User className="mr-2 h-4 w-4" />
                  <span>Профиль</span>
                </DropdownMenuItem>
                <DropdownMenuItem>
                  <Settings className="mr-2 h-4 w-4" />
                  <span>Настройки</span>
                </DropdownMenuItem>
                <DropdownMenuSeparator />
                <DropdownMenuItem asChild>
                  <Link href="/" className="flex items-center text-red-500 dark:text-red-400">
                    <LogOut className="mr-2 h-4 w-4" />
                    <span>Выйти</span>
                  </Link>
                </DropdownMenuItem>
              </DropdownMenuContent>
            </DropdownMenu>
          </div>
        </div>
      </header>

      {/* Main Content */}
      <div className="flex-1 flex">
        {/* Sidebar */}
        <aside
          className={`w-64 bg-white dark:bg-gray-900 border-r border-gray-200 dark:border-gray-800 transition-all duration-300 ${sidebarOpen ? "translate-x-0" : "-translate-x-full"} md:translate-x-0 fixed md:static inset-y-0 z-20 shadow-lg md:shadow-none`}
        >
          <div className="p-4 space-y-6">
            <div className="space-y-1">
              <Button variant="ghost" className="w-full justify-start" asChild>
                <Link href="/dashboard" className="flex items-center">
                  <Folder className="mr-2 h-5 w-5 text-blue-600 dark:text-blue-400" />
                  <span>Мои файлы</span>
                </Link>
              </Button>
              <Button variant="ghost" className="w-full justify-start">
                <FolderPlus className="mr-2 h-5 w-5 text-green-600 dark:text-green-400" />
                <span>Новая папка</span>
              </Button>
              <Button variant="ghost" className="w-full justify-start">
                <Upload className="mr-2 h-5 w-5 text-purple-600 dark:text-purple-400" />
                <span>Загрузить</span>
              </Button>
            </div>

            <div className="space-y-2">
              <h3 className="text-sm font-medium text-gray-500 dark:text-gray-400">Хранилище</h3>
              <div className="space-y-2">
                <div>
                  <div className="flex justify-between text-sm mb-1">
                    <span className="text-gray-600 dark:text-gray-300">Использовано</span>
                    <span className="font-medium">7.5 ГБ / 15 ГБ</span>
                  </div>
                  <Progress value={50} className="h-2" />
                </div>
                <Button variant="outline" size="sm" className="w-full text-xs">
                  Увеличить хранилище
                </Button>
              </div>
            </div>

            <div className="space-y-1">
              <h3 className="text-sm font-medium text-gray-500 dark:text-gray-400">Категории</h3>
              <Button variant="ghost" className="w-full justify-start text-sm">
                <File className="mr-2 h-4 w-4 text-blue-600 dark:text-blue-400" />
                <span>Документы</span>
              </Button>
              <Button variant="ghost" className="w-full justify-start text-sm">
                <File className="mr-2 h-4 w-4 text-green-600 dark:text-green-400" />
                <span>Изображения</span>
              </Button>
              <Button variant="ghost" className="w-full justify-start text-sm">
                <File className="mr-2 h-4 w-4 text-purple-600 dark:text-purple-400" />
                <span>Видео</span>
              </Button>
              <Button variant="ghost" className="w-full justify-start text-sm">
                <File className="mr-2 h-4 w-4 text-yellow-600 dark:text-yellow-400" />
                <span>Архивы</span>
              </Button>
            </div>
          </div>
        </aside>

        {/* Main Content */}
        <main className="flex-1 p-4 md:p-6 overflow-auto">
          <div className="max-w-7xl mx-auto space-y-6">
            {/* Welcome Section */}
            <div className="bg-gradient-to-r from-blue-500 to-purple-600 rounded-xl shadow-lg p-6 text-white">
              <h2 className="text-2xl font-bold mb-2">Добро пожаловать в Cloud Drive</h2>
              <p className="opacity-90 mb-4">Безопасное хранение и управление вашими файлами в облаке</p>
              <div className="flex flex-wrap gap-3">
                <Button className="bg-white text-blue-600 hover:bg-gray-100">
                  <Upload className="mr-2 h-4 w-4" />
                  Загрузить файлы
                </Button>
                <Button variant="outline" className="text-white border-white hover:bg-white/20">
                  <FolderPlus className="mr-2 h-4 w-4" />
                  Создать папку
                </Button>
              </div>
            </div>

            {/* Quick Access */}
            <div>
              <h2 className="text-xl font-bold text-gray-900 dark:text-white mb-4">Быстрый доступ</h2>
              <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-4">
                <Card className="hover:shadow-md transition-shadow cursor-pointer">
                  <CardHeader className="pb-2">
                    <CardTitle className="text-lg flex items-center">
                      <File className="h-5 w-5 text-blue-600 dark:text-blue-400 mr-2" />
                      Документы
                    </CardTitle>
                  </CardHeader>
                  <CardContent>
                    <CardDescription>120 файлов</CardDescription>
                  </CardContent>
                </Card>

                <Card className="hover:shadow-md transition-shadow cursor-pointer">
                  <CardHeader className="pb-2">
                    <CardTitle className="text-lg flex items-center">
                      <File className="h-5 w-5 text-green-600 dark:text-green-400 mr-2" />
                      Изображения
                    </CardTitle>
                  </CardHeader>
                  <CardContent>
                    <CardDescription>85 файлов</CardDescription>
                  </CardContent>
                </Card>

                <Card className="hover:shadow-md transition-shadow cursor-pointer">
                  <CardHeader className="pb-2">
                    <CardTitle className="text-lg flex items-center">
                      <File className="h-5 w-5 text-purple-600 dark:text-purple-400 mr-2" />
                      Видео
                    </CardTitle>
                  </CardHeader>
                  <CardContent>
                    <CardDescription>32 файла</CardDescription>
                  </CardContent>
                </Card>

                <Card className="hover:shadow-md transition-shadow cursor-pointer">
                  <CardHeader className="pb-2">
                    <CardTitle className="text-lg flex items-center">
                      <File className="h-5 w-5 text-yellow-600 dark:text-yellow-400 mr-2" />
                      Архивы
                    </CardTitle>
                  </CardHeader>
                  <CardContent>
                    <CardDescription>18 файлов</CardDescription>
                  </CardContent>
                </Card>
              </div>
            </div>

            {/* Recent Files */}
            <div>
              <h2 className="text-xl font-bold text-gray-900 dark:text-white mb-4">Недавние файлы</h2>
              <Card>
                <CardContent className="p-0">
                  <div className="divide-y divide-gray-200 dark:divide-gray-800">
                    <div className="flex items-center justify-between p-4 hover:bg-gray-50 dark:hover:bg-gray-800/50 transition-colors">
                      <div className="flex items-center space-x-4">
                        <File className="h-8 w-8 text-blue-600 dark:text-blue-400" />
                        <div>
                          <p className="font-medium text-gray-900 dark:text-white">Презентация проекта.pptx</p>
                          <p className="text-sm text-gray-500 dark:text-gray-400">15.2 МБ • 2 часа назад</p>
                        </div>
                      </div>
                      <Button variant="ghost" size="sm">
                        <span className="sr-only">Действия</span>
                        <svg
                          xmlns="http://www.w3.org/2000/svg"
                          width="24"
                          height="24"
                          viewBox="0 0 24 24"
                          fill="none"
                          stroke="currentColor"
                          strokeWidth="2"
                          strokeLinecap="round"
                          strokeLinejoin="round"
                          className="h-4 w-4"
                        >
                          <circle cx="12" cy="12" r="1" />
                          <circle cx="19" cy="12" r="1" />
                          <circle cx="5" cy="12" r="1" />
                        </svg>
                      </Button>
                    </div>

                    <div className="flex items-center justify-between p-4 hover:bg-gray-50 dark:hover:bg-gray-800/50 transition-colors">
                      <div className="flex items-center space-x-4">
                        <File className="h-8 w-8 text-green-600 dark:text-green-400" />
                        <div>
                          <p className="font-medium text-gray-900 dark:text-white">Финансовый отчет.xlsx</p>
                          <p className="text-sm text-gray-500 dark:text-gray-400">2.8 МБ • вчера</p>
                        </div>
                      </div>
                      <Button variant="ghost" size="sm">
                        <span className="sr-only">Действия</span>
                        <svg
                          xmlns="http://www.w3.org/2000/svg"
                          width="24"
                          height="24"
                          viewBox="0 0 24 24"
                          fill="none"
                          stroke="currentColor"
                          strokeWidth="2"
                          strokeLinecap="round"
                          strokeLinejoin="round"
                          className="h-4 w-4"
                        >
                          <circle cx="12" cy="12" r="1" />
                          <circle cx="19" cy="12" r="1" />
                          <circle cx="5" cy="12" r="1" />
                        </svg>
                      </Button>
                    </div>

                    <div className="flex items-center justify-between p-4 hover:bg-gray-50 dark:hover:bg-gray-800/50 transition-colors">
                      <div className="flex items-center space-x-4">
                        <Folder className="h-8 w-8 text-yellow-600 dark:text-yellow-400" />
                        <div>
                          <p className="font-medium text-gray-900 dark:text-white">Проекты</p>
                          <p className="text-sm text-gray-500 dark:text-gray-400">Папка • 5 файлов</p>
                        </div>
                      </div>
                      <Button variant="ghost" size="sm">
                        <span className="sr-only">Действия</span>
                        <svg
                          xmlns="http://www.w3.org/2000/svg"
                          width="24"
                          height="24"
                          viewBox="0 0 24 24"
                          fill="none"
                          stroke="currentColor"
                          strokeWidth="2"
                          strokeLinecap="round"
                          strokeLinejoin="round"
                          className="h-4 w-4"
                        >
                          <circle cx="12" cy="12" r="1" />
                          <circle cx="19" cy="12" r="1" />
                          <circle cx="5" cy="12" r="1" />
                        </svg>
                      </Button>
                    </div>
                  </div>
                </CardContent>
              </Card>
            </div>
          </div>
        </main>
      </div>
    </div>
  )
}
