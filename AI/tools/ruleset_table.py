"""Print selected rows of a transposed ruleset CSV as a compact table.

Usage: python ruleset_table.py <csv> <row1,row2,...> [column filter substring]
The ruleset CSVs are transposed: each row is a field, each column an entity.
"""
import csv
import sys


def main():
    path, rows_arg = sys.argv[1], sys.argv[2]
    col_filter = sys.argv[3] if len(sys.argv) > 3 else ""
    wanted = rows_arg.split(",")
    with open(path, encoding="utf-8") as f:
        data = {r[0]: r[1:] for r in csv.reader(f) if r}
    ids = data["id"]
    cols = [i for i, v in enumerate(ids) if v and not v.startswith("//") and col_filter in v]
    width = max(len(w) for w in wanted)
    print(" " * width, " ".join("%-14s" % ids[i].split("_", 1)[-1][:14] for i in cols))
    for w in wanted:
        vals = data.get(w)
        if vals is None:
            print("%-*s (missing)" % (width, w))
            continue
        print("%-*s" % (width, w), " ".join("%-14s" % (vals[i][:14] if i < len(vals) else "") for i in cols))


if __name__ == "__main__":
    main()
