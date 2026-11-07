#include <stdio.h>

int counter = 0;

int next() { return ++counter; }

int main() {
    int a = 7, b = -2, x, y, z;

    printf("1 - 2 - 3      = %d   (ліва асоціативність)\n", 1 - 2 - 3);
    printf("2 + 3 * 4      = %d\n", 2 + 3 * 4);
    printf("(2 + 3) * 4    = %d\n", (2 + 3) * 4);
    printf("7 / -2, 7 %% -2 = %d, %d  (ділення до нуля)\n", a / b, a % b);
    printf("-7 / 2, -7 %% 2 = %d, %d\n", -a / 2, -a % 2);
    printf("1 < 2 < 3 -> %d,  3 > 2 > 1 -> %d\n", 1 < 2 < 3, 3 > 2 > 1);
    printf("5 && 3 = %d, 0 || 7 = %d, !5 = %d\n", 5 && 3, 0 || 7, !5);
    printf("6 & 3 = %d, 6 | 3 = %d, 6 ^ 3 = %d, ~6 = %d, 1 << 4 = %d\n", 6 & 3, 6 | 3, 6 ^ 3, ~6, 1 << 4);
    printf("a > 0 ? b : 0  = %d\n", a > 0 ? b : 0);

    x = y = z = 5;
    printf("x = y = z = 5 -> %d %d %d\n", x, y, z);
    x += y *= 2;
    printf("x += y *= 2   -> x=%d y=%d\n", x, y);
    x = 10;
    y = x++;
    z = ++x;
    printf("y = x++ -> %d, z = ++x -> %d, x = %d\n", y, z, x);

    double d = 7 / 2;
    double f = 7 / 2.0;
    int t = 3.99;
    printf("7/2 = %.1f, 7/2.0 = %.1f, int t = 3.99 -> %d, (double)1/3 = %.4f\n", d, f, t, (double)1 / 3);
    printf("char 'A' + 2 = %c (%d)\n", 'A' + 2, 'A' + 2);

    if (a > 0)
        if (b > 0) printf("обидва додатні\n");
        else printf("a > 0, b <= 0  (else -> внутрішній if)\n");

    int arr[5] = {0};
    int i = 0;
    arr[i++] = next();
    arr[i++] = next();
    arr[i] = arr[0] + arr[1];
    arr[4] += arr[i]--;
    printf("arr = %d %d %d %d %d, i = %d\n", arr[0], arr[1], arr[2], arr[3], arr[4], i);
    return 0;
}
