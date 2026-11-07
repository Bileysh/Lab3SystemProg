#include <stdio.h>

int length(char s[]) {
    int n = 0;
    while (s[n] != '\0') n++;
    return n;
}

void reverse(char s[]) {
    int i = 0, j = length(s) - 1;
    while (i < j) {
        char t = s[i];
        s[i++] = s[j];
        s[j--] = t;
    }
}

void to_upper(char s[]) {
    for (int i = 0; s[i]; i++)
        if (s[i] >= 'a' && s[i] <= 'z')
            s[i] = s[i] - 'a' + 'A';
}

int is_palindrome(char s[]) {
    int n = length(s);
    for (int i = 0; i < n / 2; i++)
        if (s[i] != s[n - 1 - i]) return 0;
    return 1;
}

int main() {
    char word[] = "hello, world";
    printf("\"%s\" має довжину %d\n", word, length(word));
    reverse(word);
    printf("reverse: %s\n", word);
    to_upper(word);
    puts(word);
    char p[] = "racecar";
    printf("%s -> palindrome? %d\n", p, is_palindrome(p));
    for (char c = 'a'; c <= 'e'; c++) putchar(c);
    putchar('\n');
    return 0;
}
