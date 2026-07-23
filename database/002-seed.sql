INSERT INTO users (id, name, role) VALUES
    ('01900000-0000-7000-8000-000000000001', 'Alex Approver', 'approver'),
    ('01900000-0000-7000-8000-000000000002', 'Riley Operator', 'operator');

INSERT INTO applications (id, name) VALUES
    ('01900000-0000-7000-8000-000000000101', 'Checkout Service'),
    ('01900000-0000-7000-8000-000000000102', 'Customer Portal');

INSERT INTO application_versions (id, application_id, label) VALUES
    (
        '01900000-0000-7000-8000-000000000201',
        '01900000-0000-7000-8000-000000000101',
        '2026.7.1'
    ),
    (
        '01900000-0000-7000-8000-000000000202',
        '01900000-0000-7000-8000-000000000101',
        'release-candidate'
    ),
    (
        '01900000-0000-7000-8000-000000000203',
        '01900000-0000-7000-8000-000000000102',
        '42'
    );
