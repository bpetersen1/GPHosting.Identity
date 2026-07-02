import type {ReactNode} from 'react';
import clsx from 'clsx';
import Heading from '@theme/Heading';
import styles from './styles.module.css';

type FeatureItem = {
  title: string;
  description: ReactNode;
};

const FeatureList: FeatureItem[] = [
  {
    title: '.NET 10 / C# 13 native',
    description: (
      <>
        No legacy shims, no <code>netstandard2.0</code> baggage. Trim-and-AOT-ready,{' '}
        <code>System.Text.Json</code> throughout — no Newtonsoft.Json dependency.
      </>
    ),
  },
  {
    title: 'Modern OAuth standards',
    description: (
      <>
        Pushed Authorization Requests (RFC 9126), DPoP (RFC 9449), JARM, and a path toward
        FAPI 2.0 — protocol features the original IdentityServer4 never shipped.
      </>
    ),
  },
  {
    title: 'Security-tested, not just reviewed',
    description: (
      <>
        PKCE enforcement, redirect URI validation, and secret hashing all ship with tests that
        prove the attack actually fails — not just that the happy path works.
      </>
    ),
  },
];

function Feature({title, description}: FeatureItem) {
  return (
    <div className={clsx('col col--4')}>
      <div className="text--center padding-horiz--md">
        <Heading as="h3">{title}</Heading>
        <p>{description}</p>
      </div>
    </div>
  );
}

export default function HomepageFeatures(): ReactNode {
  return (
    <section className={styles.features}>
      <div className="container">
        <div className="row">
          {FeatureList.map((props, idx) => (
            <Feature key={idx} {...props} />
          ))}
        </div>
      </div>
    </section>
  );
}
