#include <stdio.h>

int fact(int n) {
    if (n <= 1)
        return 1;
    else
        return n * fact(n - 1);
}

int fact_iter(int n) {
    int r = 1;
    for (int i = 2; i <= n; i++)
        r *= i;
    return r;
}

int main() {
    for (int i = 0; i <= 10; i++) {
        printf("%d! = %d (iter: %d)\n", i, fact(i), fact_iter(i));
    }
    return 0;
}
