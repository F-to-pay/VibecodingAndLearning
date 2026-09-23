# Простой Makefile: `make` соберёт бинарник, `make clean` его уберёт.
# Работает и на Linux/macOS, и на Windows (MinGW + mingw32-make).

CC = gcc
CFLAGS = -Wall -Wextra -std=c11

# На Windows переменная окружения OS всегда равна "Windows_NT"
# (её выставляет сама система), на Linux/macOS её обычно нет вообще.
# По ней и понимаем, где собираемся.
ifeq ($(OS),Windows_NT)
    TARGET = open_tabs.exe
    # shell32 — там живёт ShellExecuteA(), ole32 — CoInitializeEx().
    # gcc на Windows сам их не подключает, в отличие от MSVC с его #pragma.
    LDLIBS = -lshell32 -lole32
    # В cmd.exe нет `rm`, есть `del`. 2>nul — "молчи, если файла нет".
    RM = del /Q 2>nul
else
    TARGET = open_tabs
    LDLIBS =
    RM = rm -f
endif

$(TARGET): main.c
	$(CC) $(CFLAGS) -o $(TARGET) main.c $(LDLIBS)

clean:
	$(RM) $(TARGET)

.PHONY: clean
