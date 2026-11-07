#include <stdio.h>
#define N 10

void bubble_sort(int a[], int n) {
    for (int i = 0; i < n - 1; i++) {
        int swapped = 0;
        for (int j = 0; j < n - 1 - i; j++) {
            if (a[j] > a[j + 1]) {
                int t = a[j];
                a[j] = a[j + 1];
                a[j + 1] = t;
                swapped = 1;
            }
        }
        if (!swapped) break;
    }
}

int binary_search(int a[], int n, int key) {
    int lo = 0, hi = n - 1;
    while (lo <= hi) {
        int mid = (lo + hi) / 2;
        if (a[mid] == key) return mid;
        else if (a[mid] < key) lo = mid + 1;
        else hi = mid - 1;
    }
    return -1;
}

void print_array(int a[], int n) {
    for (int i = 0; i < n; i++)
        printf("%d%s", a[i], i < n - 1 ? ", " : "\n");
}

int main() {
    int data[N] = {42, 7, -3, 19, 0, 88, 23, 5, 11, -10};
    printf("До сортування:    ");
    print_array(data, N);
    bubble_sort(data, N);
    printf("Після сортування: ");
    print_array(data, N);
    printf("Індекс 19: %d, індекс 100: %d\n", binary_search(data, N, 19), binary_search(data, N, 100));
    return 0;
}
