# Простой Makefile: `make` соберёт бинарник, `make clean` его уберёт.

CC = gcc
CFLAGS = -Wall -Wextra -std=c11
TARGET = open_tabs

$(TARGET): main.c
	$(CC) $(CFLAGS) -o $(TARGET) main.c

clean:
	rm -f $(TARGET)

.PHONY: clean
