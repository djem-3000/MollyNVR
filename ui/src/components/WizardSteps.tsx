import { t } from '../i18n'
import { STEP_LABELS, type WizardStep } from '../wizardTypes'

type Props = {
  current: WizardStep
  onNavigate: (step: WizardStep) => void
}

const STEPS: WizardStep[] = [1, 2, 3, 4]

export default function WizardSteps({ current, onNavigate }: Props) {
  return (
    <nav aria-label={t('wizard.steps')}>
      <ol className="wizard-steps">
        {STEPS.map((step) => {
          const state = step < current ? 'done' : step === current ? 'current' : 'upcoming'
          const label = t(STEP_LABELS[step])
          const content = (
            <>
              <span className="wizard-step-number">{step}</span>
              <span className="wizard-step-label">{label}</span>
            </>
          )

          return (
            <li key={step} className={`wizard-step ${state}`}>
              {step < current ? (
                <button
                  type="button"
                  className="wizard-step-link"
                  aria-label={t('wizard.steps.goTo', { step, label })}
                  onClick={() => onNavigate(step)}
                >
                  {content}
                </button>
              ) : (
                <span
                  className="wizard-step-link"
                  aria-current={step === current ? 'step' : undefined}
                >
                  {content}
                </span>
              )}
            </li>
          )
        })}
      </ol>
    </nav>
  )
}
