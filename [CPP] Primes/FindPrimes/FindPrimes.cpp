#include <iostream>
#include <list> 
#include <iterator> 

class Range
{
private:
    unsigned long long start;
    unsigned long long end;

public:
    Range(unsigned long long start, unsigned long long end)
    {
        this->start = start;
        this->end = end;
    }

    unsigned long long getStart() { return start; }
    unsigned long long getEnd() { return end; }
};

int main()
{
    int n;
    std::list<Range> ranges;

    std::cout << "Unesite zeljeni broj opsega: ";
    std::cin >> n;

    for (int i = 1; i <= n; i++)
    {
        unsigned long long start = 0;
        unsigned long long end = 0;
        std::cout << std::endl << "Opseg " << i << ":" << std::endl << " Start = ";
        std::cin >> start;
        std::cout << " End = ";
        std::cin >> end;
        Range r(start, end);
        ranges.push_back(r);
    }

    unsigned long long counter = 0;

    for (Range r : ranges) 
    {
        for (unsigned long long i = r.getStart(); i <= r.getEnd(); i++)
        {
            bool prime = true;
            for (unsigned long long j = 2; j < i / 2 + 1; j++)
                if (i % j == 0)
                {
                    prime = false;
                    break;
                }

            if (prime && i >= 2)
                counter++;
        }
    }

    std::cout << std::endl << "Ukupno prostih brojeva u opsezima: " << counter << std::endl;
}


