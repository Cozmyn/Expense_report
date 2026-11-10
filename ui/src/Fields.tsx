import React from 'react';
export function Field({
  name,
  children,
  note,
  action,
}: {
  name: string;
  children: React.ReactNode;
  note?: string;
  action?: React.ReactNode;
}) {
  const id = React.useId();
  return (
    <div className="field">
      <div className="field-label-row">
        <label htmlFor={id}>{name}</label>
        {action}
      </div>
      {React.isValidElement(children)
        ? React.cloneElement(children as React.ReactElement<any>, {
            id,
            'aria-describedby': note ? id + '-note' : undefined,
          })
        : children}
      {note && <small id={id + '-note'}>{note}</small>}
    </div>
  );
}
