#include <stdio.h>

int x = 100;

void show(int x) {
    printf("  параметр x = %d\n", x);
}

int add_global(int d) {
    x += d;
    return x;
}

int main() {
    printf("глобальна x = %d\n", x);
    int x = 1;
    printf("локальна x = %d\n", x);
    {
        int x = 2;
        printf("  внутрішній блок x = %d\n", x);
        {
            int x = 3;
            printf("    ще глибше x = %d\n", x);
        }
        printf("  знову x = %d\n", x);
    }
    printf("локальна x = %d\n", x);
    show(42);
    printf("add_global(5) = %d\n", add_global(5));

    int sum = 0;
    for (int i = 0; i < 10; i++) {
        if (i % 2 == 0) continue;
        sum += i;
    }
    printf("сума непарних до 10 = %d\n", sum);

    int n = 0;
    do {
        n++;
        if (n == 3) continue;
        printf("n = %d\n", n);
    } while (n < 5);
    return 0;
}
