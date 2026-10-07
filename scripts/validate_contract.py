#!/usr/bin/env python3
import json, sys
import jsonschema
schema = json.load(open(sys.argv[1],encoding='utf-8-sig'))
jsonschema.Draft202012Validator.check_schema(schema)
validator = jsonschema.Draft202012Validator(schema, format_checker=jsonschema.FormatChecker())
count = 0
for path in sys.argv[2:]:
    for n,line in enumerate(open(path,encoding='utf-8-sig'),1):
        event=json.loads(line)
        errors=list(validator.iter_errors(event))
        if errors:
            raise SystemExit(f'{path}:{n}: {errors[0].json_path}: {errors[0].message}')
        count += 1
print(f'SCHEMA PASS events={count}')
