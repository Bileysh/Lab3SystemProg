#include <stdio.h>

int sieve[101];

int gcd(int a, int b) {
    while (b != 0) {
        int t = a % b;
        a = b;
        b = t;
    }
    return a;
}

int fib(int n) {
    int a = 0, b = 1;
    do {
        int c = a + b;
        a = b;
        b = c;
    } while (--n > 0);
    return a;
}

int main() {
    int count = 0;
    for (int i = 2; i <= 100; i++) sieve[i] = 1;
    for (int i = 2; i * i <= 100; i++)
        if (sieve[i])
            for (int j = i * i; j <= 100; j += i)
                sieve[j] = 0;

    printf("Прості числа до 100: ");
    for (int i = 2; i <= 100; i++) {
        if (!sieve[i]) continue;
        printf("%d ", i);
        count++;
    }
    printf("\nВсього: %d\n", count);

    printf("gcd(84, 36) = %d, lcm(84, 36) = %d\n", gcd(84, 36), 84 / gcd(84, 36) * 36);
    for (int k = 1; k <= 15; k++)
        printf("fib(%d) = %d\n", k, fib(k));
    return 0;
}
