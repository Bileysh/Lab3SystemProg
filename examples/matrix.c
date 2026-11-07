#include <stdio.h>
#include <math.h>

#define SIZE 3

void mat_mul(double a[][SIZE], double b[][SIZE], double c[][SIZE]) {
    for (int i = 0; i < SIZE; i++)
        for (int j = 0; j < SIZE; j++) {
            c[i][j] = 0;
            for (int k = 0; k < SIZE; k++)
                c[i][j] += a[i][k] * b[k][j];
        }
}

double det3(double m[][SIZE]) {
    return m[0][0] * (m[1][1] * m[2][2] - m[1][2] * m[2][1])
         - m[0][1] * (m[1][0] * m[2][2] - m[1][2] * m[2][0])
         + m[0][2] * (m[1][0] * m[2][1] - m[1][1] * m[2][0]);
}

int main() {
    double a[SIZE][SIZE] = {{1, 2, 3}, {0, 1, 4}, {5, 6, 0}};
    double e[SIZE][SIZE] = {{1, 0, 0}, {0, 1, 0}, {0, 0, 1}};
    double c[SIZE][SIZE];

    mat_mul(a, a, c);
    printf("A * A =\n");
    for (int i = 0; i < SIZE; i++) {
        for (int j = 0; j < SIZE; j++)
            printf("%8.2f", c[i][j]);
        printf("\n");
    }
    mat_mul(a, e, c);
    printf("det(A) = %.3f, det(A*E) = %.3f\n", det3(a), det3(c));
    printf("sqrt(2) = %.6f, pow(2, 10) = %.0f\n", sqrt(2.0), pow(2, 10));
    return 0;
}
