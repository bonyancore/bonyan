import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { ScrollView, View } from 'react-native';
import { useSafeAreaInsets } from 'react-native-safe-area-context';

import {
  ActivityItem,
  AppBar,
  ApprovalCard,
  BottomSheet,
  Button,
  ChartContainer,
  ContextSwitcher,
  CriticalAlertCard,
  DatePicker,
  Dialog,
  EmptyState,
  ErrorState,
  FloatingActionButton,
  FloatingTabBar,
  FormSection,
  GlassCard,
  Icon,
  KpiCard,
  ListRow,
  LoadingState,
  NavigationDrawer,
  OfflineState,
  PressableScale,
  ProgressBar,
  ProgressRing,
  ProjectCard,
  QuickActionTile,
  ScopeSelector,
  SearchField,
  SectionContainer,
  SegmentedProgress,
  SelectField,
  SolidSurface,
  StatusBadge,
  TaskCard,
  Text,
  TextField,
  TimelineItem,
  Toast,
  type DrawerNavItem,
  type ScopeOption,
  type TabItem,
} from '@/components/ui';
import { changeLanguage } from '@/core/i18n';
import { isRTL } from '@/core/i18n/direction';
import { usePreferences } from '@/core/preferences/store';
import { colors, layout } from '@/design/tokens';

export default function DesignPreview() {
  const { t } = useTranslation();
  const insets = useSafeAreaInsets();
  const language = usePreferences((state) => state.language);
  const rtl = isRTL();

  // Navigation & Scope
  const [activeTab, setActiveTab] = useState('home');
  const [activeScope, setActiveScope] = useState<ScopeOption>('project');
  const [viewMode, setViewMode] = useState<'dashboard' | 'components'>('dashboard');
  const [appBarVariant, setAppBarVariant] = useState<'large' | 'compact'>('large');

  // Overlays
  const [contextSwitcherOpen, setContextSwitcherOpen] = useState(false);
  const [createSheetOpen, setCreateSheetOpen] = useState(false);
  const [drawerOpen, setDrawerOpen] = useState(false);
  const [dialogOpen, setDialogOpen] = useState(false);
  const [toastMessage, setToastMessage] = useState<string | null>(null);

  // Selections
  const [selectedCompany, setSelectedCompany] = useState('c1');
  const [selectedProject, setSelectedProject] = useState('p1');
  const [activeChartRange, setActiveChartRange] = useState('12W');

  // Form states
  const [searchQuery, setSearchQuery] = useState('');
  const [activeSearchFilter, setActiveSearchFilter] = useState('all');
  const [trade, setTrade] = useState('concrete');
  const [neededDate, setNeededDate] = useState('24 Nov 2026');

  const tabs: TabItem[] = [
    { key: 'home', label: t('nav.home'), icon: 'home' },
    { key: 'projects', label: t('nav.projects'), icon: 'projects' },
    { key: 'tasks', label: t('nav.tasks'), icon: 'tasks' },
    { key: 'approvals', label: t('nav.approvals'), icon: 'approvals', badge: 3 },
  ];

  const companies = [
    { id: 'c1', name: 'Delta Build Co.', initials: 'DB', role: rtl ? 'مدير المشروع' : 'Project manager' },
    { id: 'c2', name: 'Nasr Contracting', initials: 'NC', role: rtl ? 'مهندس موقع' : 'Site engineer' },
  ];

  const projects = [
    { id: 'all', name: rtl ? 'جميع المشاريع' : 'All projects', statusDotColor: colors.foregroundMuted },
    { id: 'p1', name: rtl ? 'برج بوابة النيل' : 'Nile Gate Towers', progress: 58, statusDotColor: colors.action },
    { id: 'p2', name: rtl ? 'مركز الرياض اللوجستي' : 'Riyadh Logistics Hub', progress: 31, statusDotColor: colors.chartMint },
    { id: 'p3', name: rtl ? 'سكن المعادي' : 'Maadi Residence', progress: 82, statusDotColor: colors.danger },
  ];

  const drawerItems: DrawerNavItem[] = [
    { key: 'home', label: t('nav.home'), icon: 'home' },
    { key: 'projects', label: t('nav.projects'), icon: 'projects' },
    { key: 'tasks', label: t('nav.tasks'), icon: 'tasks' },
    { key: 'procurement', label: t('nav.procurement'), icon: 'procurement' },
    { key: 'finance', label: t('nav.finance'), icon: 'finance' },
    { key: 'reports', label: t('nav.reports'), icon: 'fileText' },
  ];

  const chartSeries = [
    { name: rtl ? 'تدفقات واردة' : 'Money in', color: colors.chartGlacier, data: [1.2, 1.4, 1.8, 1.6, 2.4, 1.9] },
    { name: rtl ? 'تدفقات خارجة' : 'Money out', color: colors.chartEmber, data: [0.9, 1.1, 1.0, 1.2, 1.8, 1.5] },
  ];

  const activeCompanyName = companies.find((c) => c.id === selectedCompany)?.name || 'Delta Build';
  const activeCompanyInitials = companies.find((c) => c.id === selectedCompany)?.initials || 'DB';
  const activeProjectName = projects.find((p) => p.id === selectedProject)?.name || 'Nile Gate Towers';

  return (
    <View className="flex-1" style={{ backgroundColor: colors.background }}>
      {/* App Bar matching Dashboard board */}
      <AppBar
        variant={appBarVariant}
        title={appBarVariant === 'compact' ? activeProjectName : rtl ? 'صباح الخير يا عمر' : 'Good morning, Omar'}
        subtitle={appBarVariant === 'compact' ? (rtl ? 'تفاصيل المشروع' : 'Project detail') : rtl ? 'الأربعاء 18 نوفمبر' : 'Wednesday 18 November'}
        companyName={activeCompanyName}
        companyAvatar={activeCompanyInitials}
        projectName={activeProjectName}
        onCompanyPress={() => setContextSwitcherOpen(true)}
        onSearchPress={() => setToastMessage(rtl ? 'تم تفعيل البحث' : 'Search activated')}
        onNotificationPress={() => setToastMessage(rtl ? 'تنبيه حرج جديد' : '1 critical alert')}
        hasUnreadNotifications={1}
        onBackPress={() => setAppBarVariant('large')}
        onOptionsPress={() => setCreateSheetOpen(true)}
      >
        {appBarVariant === 'large' ? (
          <ScopeSelector
            activeScope={activeScope}
            onSelectScope={setActiveScope}
            options={[
              { key: 'company', label: t('scope.company') },
              { key: 'project', label: t('scope.project') },
              { key: 'mine', label: t('scope.mine') },
            ]}
          />
        ) : null}
      </AppBar>

      {/* Main Scroll Content */}
      <ScrollView
        contentContainerClassName="gap-6 px-5"
        contentContainerStyle={{
          paddingTop: 8,
          paddingBottom: insets.bottom + layout.listBottomPadding,
        }}
        showsVerticalScrollIndicator={false}
      >
        {/* View Toggle Bar (Interactive system inspector) */}
        <View className="flex-row items-center justify-between border-b border-line pb-3">
          <View className="flex-row items-center gap-2">
            <PressableScale
              onPress={() => setViewMode('dashboard')}
              style={{
                backgroundColor: viewMode === 'dashboard' ? colors.raised : 'transparent',
                borderWidth: 1,
                borderColor: viewMode === 'dashboard' ? colors.actionRing : colors.line,
                borderRadius: layout.touchTarget / 2,
                paddingHorizontal: 12,
                paddingVertical: 6,
              }}
            >
              <Text variant="caption" tone={viewMode === 'dashboard' ? 'action' : 'muted'} style={{ fontWeight: '600' }}>
                {rtl ? 'لوحة القيادة' : 'Dashboard'}
              </Text>
            </PressableScale>

            <PressableScale
              onPress={() => setViewMode('components')}
              style={{
                backgroundColor: viewMode === 'components' ? colors.raised : 'transparent',
                borderWidth: 1,
                borderColor: viewMode === 'components' ? colors.actionRing : colors.line,
                borderRadius: layout.touchTarget / 2,
                paddingHorizontal: 12,
                paddingVertical: 6,
              }}
            >
              <Text variant="caption" tone={viewMode === 'components' ? 'action' : 'muted'} style={{ fontWeight: '600' }}>
                {rtl ? 'مكتبة المكونات' : 'All Components'}
              </Text>
            </PressableScale>
          </View>

          <View className="flex-row items-center gap-2">
            <PressableScale
              onPress={() => changeLanguage(language === 'en' ? 'ar' : 'en')}
              className="rounded-chip bg-surface px-2.5 py-1.5 border border-line"
            >
              <Text variant="caption" tone="soft" style={{ fontWeight: '600' }}>
                {language === 'en' ? 'العربية (RTL)' : 'English (LTR)'}
              </Text>
            </PressableScale>

            <PressableScale
              onPress={() => setAppBarVariant(appBarVariant === 'large' ? 'compact' : 'large')}
              className="rounded-chip bg-surface px-2.5 py-1.5 border border-line"
            >
              <Text variant="caption" tone="muted">
                {appBarVariant === 'large' ? 'Compact Bar' : 'Large Bar'}
              </Text>
            </PressableScale>
          </View>
        </View>

        {viewMode === 'dashboard' ? (
          <>
            {/* 1. Hero KPI Cards */}
            <View className="flex-row gap-3">
              <View className="flex-1">
                <KpiCard
                  label={rtl ? 'السيولة المتاحة' : 'Cash position'}
                  value="12.4M"
                  delta={{ label: rtl ? '3.2% ↗' : '↗ 3.2%', status: 'onTrack' }}
                  trendData={[10.5, 11, 10.8, 11.5, 11.9, 12.4]}
                />
              </View>

              <View className="flex-1">
                <KpiCard
                  label={rtl ? 'تجاوزات مفتوحة' : 'Open overruns'}
                  value="2"
                  delta={{ label: rtl ? '1 إضافية' : '1 more', status: 'blocked' }}
                  progress={{
                    label: rtl ? 'تقدم المشروع' : 'Project progress',
                    value: 58,
                    plan: 64,
                  }}
                />
              </View>
            </View>

            {/* 2. Critical alerts (Sitting above approvals, max 3) */}
            <SectionContainer
              title={rtl ? 'تنبيهات حرجة' : 'Critical alerts'}
              badgeCount={1}
              badgeTone="danger"
            >
              <CriticalAlertCard
                title={rtl ? 'فشل فحص شدة السقف، المبنى C' : 'Formwork inspection failed, Block C'}
                subtitle={rtl ? 'يوقف صب البلاطة · منذ ساعتين' : 'Blocks slab pour · 2 h ago'}
                onPress={() => setToastMessage(rtl ? 'تم فتح تفاصيل الفحص' : 'Opening inspection detail')}
              />
            </SectionContainer>

            {/* 3. Waiting on you (Approvals) */}
            <SectionContainer
              title={rtl ? 'بانتظار موافقتك' : 'Waiting on you'}
              actionLabel={t('action.seeAll')}
              onAction={() => setToastMessage(rtl ? 'فتح قائمة الموافقات' : 'Opening Approvals inbox')}
              solidBody
            >
              <ListRow
                leading={
                  <View className="h-9 w-9 items-center justify-center rounded-control bg-raised">
                    <Icon name="fileText" size={18} color={colors.action} />
                  </View>
                }
                title={rtl ? 'أسمنت، 240 كيس' : 'Cement, 240 bags'}
                subtitle={rtl ? '4 عروض أسعار · منذ يومين' : '4 quotes · 2 days'}
                onPress={() => setDialogOpen(true)}
              />
              <View className="h-px bg-line ms-14" />
              <ListRow
                leading={
                  <View className="h-9 w-9 items-center justify-center rounded-control bg-raised">
                    <Icon name="finance" size={18} color={colors.action} />
                  </View>
                }
                title={rtl ? 'شهادة دفع رقم 7' : 'Payment certificate 7'}
                subtitle={rtl ? 'المقاول الباطن للكهرباء · اليوم' : 'MEP subcontractor · today'}
                onPress={() => setDialogOpen(true)}
              />
            </SectionContainer>

            {/* 4. Below the fold: Project performance */}
            <SectionContainer
              title={rtl ? 'أداء المشاريع' : 'Project performance'}
              actionLabel={t('action.seeAll')}
              onAction={() => setToastMessage(rtl ? 'فتح المشاريع' : 'Opening projects')}
              solidBody
            >
              <View className="gap-4 p-4">
                <View className="gap-1.5">
                  <View className="flex-row items-center justify-between">
                    <Text variant="footnote" tone="default">
                      {rtl ? 'برج بوابة النيل' : 'Nile Gate Towers'}
                    </Text>
                    <Text variant="footnote" tone="soft">
                      58%
                    </Text>
                  </View>
                  <ProgressBar value={58} plan={64} tone="warning" label="Nile Gate Towers" />
                </View>

                <View className="gap-1.5">
                  <View className="flex-row items-center justify-between">
                    <Text variant="footnote" tone="default">
                      {rtl ? 'مركز الرياض اللوجستي' : 'Riyadh Logistics Hub'}
                    </Text>
                    <Text variant="footnote" tone="soft">
                      31%
                    </Text>
                  </View>
                  <ProgressBar value={31} plan={35} tone="info" label="Riyadh Logistics Hub" />
                </View>

                <View className="gap-1.5">
                  <View className="flex-row items-center justify-between">
                    <Text variant="footnote" tone="default">
                      {rtl ? 'سكن المعادي' : 'Maadi Residence'}
                    </Text>
                    <Text variant="footnote" tone="soft">
                      82%
                    </Text>
                  </View>
                  <ProgressBar value={82} plan={80} tone="warning" label="Maadi Residence" />
                </View>
              </View>
            </SectionContainer>

            {/* 5. Below the fold: Recent activity */}
            <SectionContainer title={rtl ? 'النشاط الأخير' : 'Recent activity'} solidBody>
              <View className="p-3">
                <ActivityItem
                  icon="check"
                  statusTone="success"
                  actor={rtl ? 'كريم أكمل' : 'Karim completed'}
                  target={rtl ? 'صب سقف الدور الثالث' : 'Pour slab, level 3'}
                  time="09:40"
                  onPress={() => setToastMessage('Activity selected')}
                />
                <View className="h-px bg-line my-1" />
                <ActivityItem
                  icon="cube"
                  statusTone="info"
                  actor={rtl ? 'تم استلام التوريد:' : 'Delivery received:'}
                  target={rtl ? 'حديد تسليح 16 مم، 12 طن' : 'rebar 16 mm, 12 t'}
                  time="08:15"
                  onPress={() => setToastMessage('Activity selected')}
                />
                <View className="h-px bg-line my-1" />
                <ActivityItem
                  icon="warning"
                  statusTone="danger"
                  actor={rtl ? 'فشل الفحص:' : 'Inspection failed:'}
                  target={rtl ? 'محاذاة شدة السقف' : 'formwork alignment'}
                  time={rtl ? 'أمس' : 'Yesterday'}
                  onPress={() => setToastMessage('Activity selected')}
                />
              </View>
            </SectionContainer>

            {/* 6. Below the fold: Quick actions */}
            <SectionContainer title={rtl ? 'إجراءات سريعة' : 'Quick actions'}>
              <View className="flex-row gap-3">
                <QuickActionTile
                  icon="fileText"
                  label={t('action.report')}
                  onPress={() => setCreateSheetOpen(true)}
                />
                <QuickActionTile
                  icon="cube"
                  label={t('action.request')}
                  onPress={() => setCreateSheetOpen(true)}
                />
                <QuickActionTile
                  icon="approvals"
                  label={t('action.approve')}
                  onPress={() => setDialogOpen(true)}
                />
              </View>
            </SectionContainer>
          </>
        ) : (
          /* ========================================================================= */
          /* COMPLETE DESIGN SYSTEM COMPONENT LIBRARY SHOWCASE (ALL BOARDS VERIFIED)   */
          /* ========================================================================= */
          <>
            {/* SURFACES */}
            <SectionContainer title="Surfaces (Obsidian Liquid)">
              <GlassCard style={{ padding: 16 }}>
                <Text variant="headline">GlassCard (Hero Surface)</Text>
                <Text variant="caption" tone="soft" className="mt-1">
                  Hero moments only: one per screen, short text, over ambient light.
                </Text>
              </GlassCard>

              <SolidSurface style={{ padding: 16 }}>
                <Text variant="headline">SolidSurface</Text>
                <Text variant="caption" tone="soft" className="mt-1">
                  Fill #10161D, no blur, radius 16. For forms, dense data, financial and critical content.
                </Text>
              </SolidSurface>

              <OfflineState queueCount={3} />
            </SectionContainer>

            {/* STATUS BADGES */}
            <SectionContainer title="Status Badges (Icon + Label, 14% tints)">
              <View className="flex-row flex-wrap gap-2.5">
                <StatusBadge status="onTrack" label="On track" />
                <StatusBadge status="inProgress" label="In progress" />
                <StatusBadge status="atRisk" label="At risk" />
                <StatusBadge status="blocked" label="Blocked" />
                <StatusBadge status="draft" label="Draft" />
              </View>
              <View className="flex-row items-center gap-3 mt-1">
                <StatusBadge status="atRisk" label="At risk (compact)" size="compact" />
                <StatusBadge status="blocked" label="Dense row dot" variant="dot" />
                <StatusBadge status="atRisk" count={3} variant="count" />
              </View>
            </SectionContainer>

            {/* WORKFLOW CARDS */}
            <SectionContainer title="Workflow Cards">
              <ApprovalCard
                code="MR-0142"
                waitingTime="Waiting 2 days"
                title="Cement, 240 bags, 4 quotes compared"
                amount="EGP 148,800"
                onApprove={() => setDialogOpen(true)}
                onReject={() => setToastMessage('Purchase order rejected')}
              />

              <TaskCard
                code="T-218"
                status="atRisk"
                statusLabel="Due today"
                title="Pour slab, Block C level 4"
                trade="Concrete"
                assigneeInitials="KA"
                assigneeName="Karim"
                progressText="3 of 5 checks"
                actionItemLabel="Set out column grid"
                onCompleteAction={() => setToastMessage('Checklist item toggled')}
              />

              <ProjectCard
                name="Nile Gate Towers"
                typeSubtitle="New Cairo · 3 blocks"
                status="atRisk"
                statusLabel="At risk"
                progress={58}
                plan={64}
                tasksCount={12}
                approvalsCount={3}
                onPress={() => setToastMessage('Project clicked')}
              />
            </SectionContainer>

            {/* DATA & ANALYTICS */}
            <SectionContainer title="Data & Analytics">
              <ChartContainer
                title="Cash in and out"
                subtitle="Nile Gate Towers · weekly"
                series={chartSeries}
                categories={['W1', 'W4', 'W6', 'W8', 'W10', 'W12']}
                ranges={['4W', '12W', '26W']}
                activeRange={activeChartRange}
                onRangeChange={setActiveChartRange}
                sourceNote="Source: payment certificates and POs · updated 06:00"
              />

              <View className="flex-row items-center justify-around rounded-card bg-surface p-4 border border-line">
                <View className="items-center gap-1.5">
                  <ProgressRing value={61} color={colors.info} subtitle="Overall" />
                  <Text variant="caption" tone="soft">
                    ProgressRing (61%)
                  </Text>
                </View>
                <View className="items-center gap-2">
                  <Text variant="caption" tone="muted">
                    SegmentedProgress (3/5)
                  </Text>
                  <View style={{ width: 140 }}>
                    <SegmentedProgress total={5} current={3} tone="info" />
                  </View>
                </View>
              </View>
            </SectionContainer>

            {/* FORMS & INPUTS */}
            <SectionContainer title="Forms & Inputs (52px controls)">
              <FormSection title="Create Material Request" statusLabel="2 of 3 filled">
                <TextField
                  label="Quantity"
                  value="240"
                  unit="bags"
                  hint="Numeric field with tabular digits"
                />
                <TextField
                  label="Unit"
                  value="bags"
                  error="Use tonnes for this item"
                />
                <TextField
                  label="Cost code, locked"
                  value="03-3000"
                  readOnly
                />
                <SearchField
                  value={searchQuery}
                  onChangeText={setSearchQuery}
                  filters={[
                    { key: 'all', label: 'All' },
                    { key: 'overdue', label: 'Overdue' },
                    { key: 'mine', label: 'Mine' },
                  ]}
                  activeFilter={activeSearchFilter}
                  onFilterChange={setActiveSearchFilter}
                />
                <SelectField
                  label="Trade"
                  value={trade}
                  options={[
                    { value: 'concrete', label: 'Concrete works' },
                    { value: 'rebar', label: 'Steel & Rebar' },
                    { value: 'mep', label: 'MEP Electrical' },
                    { value: 'plumbing', label: 'Plumbing & Drainage' },
                    { value: 'drywall', label: 'Drywall & Partitions' },
                    { value: 'painting', label: 'Painting & Finishing' },
                    { value: 'flooring', label: 'Flooring & Tiling' },
                    { value: 'facade', label: 'Curtain Wall & Facade' },
                    { value: 'landscaping', label: 'External Landscaping' },
                  ]}
                  onSelect={setTrade}
                />
                <DatePicker
                  label="Needed by"
                  value={neededDate}
                  onChangeDate={setNeededDate}
                />
              </FormSection>
            </SectionContainer>

            {/* TIMELINE & MILESTONES */}
            <SectionContainer title="Timeline (Done, Current, Upcoming)">
              <SolidSurface style={{ padding: 16 }}>
                <TimelineItem title="Requested" subtitle="Omar · 16 Nov" state="completed" />
                <TimelineItem title="Quotes compared" subtitle="Procurement · 17 Nov" state="completed" />
                <TimelineItem
                  title="Waiting for approval"
                  subtitle="Financial controller · now"
                  state="active"
                />
                <TimelineItem title="Purchase order issued" state="pending" isLast />
              </SolidSurface>
            </SectionContainer>

            {/* BUTTONS & CONTROLS */}
            <SectionContainer title="Buttons & Controls">
              <View className="flex-row gap-3">
                <View className="flex-1">
                  <Button label="Primary Button" variant="primary" onPress={() => setDialogOpen(true)} />
                </View>
                <View className="flex-1">
                  <Button label="Secondary" variant="secondary" onPress={() => setToastMessage('Secondary button')} />
                </View>
              </View>

              <View className="flex-row gap-3 mt-1">
                <View className="flex-1">
                  <Button label="Destructive" variant="destructive" onPress={() => setToastMessage('Destructive')} />
                </View>
                <View className="flex-1">
                  <Button label="Compact size" size="compact" variant="primary" onPress={() => {}} />
                </View>
              </View>

              <View className="flex-row items-center justify-around py-2">
                <FloatingActionButton
                  size="standard"
                  accessibilityLabel="New"
                  onPress={() => setCreateSheetOpen(true)}
                />
                <FloatingActionButton
                  size="mini"
                  accessibilityLabel="Mini"
                  onPress={() => setCreateSheetOpen(true)}
                />
                <FloatingActionButton
                  size="extended"
                  label="+ New request"
                  accessibilityLabel="New request"
                  onPress={() => setCreateSheetOpen(true)}
                />
              </View>
            </SectionContainer>

            {/* FEEDBACK & EMPTY STATES */}
            <SectionContainer title="Feedback & Error States">
              <EmptyState
                title="Nothing waiting on you"
                subtitle="New approvals will show here."
              />
              <ErrorState
                title="Could not load tasks"
                subtitle="Your changes are safe."
                onRetry={() => setToastMessage('Retrying tasks...')}
              />
              <LoadingState count={1} />
            </SectionContainer>
          </>
        )}
      </ScrollView>

      {/* Persistent Toast & Snackbar */}
      {toastMessage ? (
        <View className="absolute top-20 start-0 end-0 z-50">
          <Toast
            message={toastMessage}
            actionLabel="Dismiss"
            onAction={() => setToastMessage(null)}
          />
        </View>
      ) : null}

      {/* Floating Tab Bar matching the design board */}
      <FloatingTabBar
        tabs={tabs}
        activeKey={activeTab}
        createLabel={t('nav.create')}
        onSelect={(key) => {
          setActiveTab(key);
          if (key === 'approvals') {
            setToastMessage('Approvals tab selected');
          }
        }}
        onCreate={() => setCreateSheetOpen(true)}
      />

      {/* Context Switcher Bottom Sheet matching Context switcher.png */}
      <ContextSwitcher
        visible={contextSwitcherOpen}
        onClose={() => setContextSwitcherOpen(false)}
        companies={companies}
        selectedCompanyId={selectedCompany}
        onSelectCompany={setSelectedCompany}
        projects={projects}
        selectedProjectId={selectedProject}
        onSelectProject={setSelectedProject}
      />

      {/* Quick Create Bottom Sheet matching Quick create.png */}
      <BottomSheet
        visible={createSheetOpen}
        onClose={() => setCreateSheetOpen(false)}
        title={rtl ? 'إنشاء' : 'Create'}
        footerNote={rtl ? 'يتم عرض الإجراءات المتاحة لدورك فقط.' : 'Only actions your role can use are listed.'}
      >
        <View className="gap-2.5 pb-4">
          <PressableScale
            onPress={() => {
              setCreateSheetOpen(false);
              setToastMessage(rtl ? 'تم اختيار تقرير يومي' : 'Creating Daily report');
            }}
            className="flex-row items-center gap-3 rounded-control bg-surface p-3.5 border border-line"
          >
            <Icon name="camera" size={20} color={colors.action} />
            <Text variant="bodyStrong">
              {rtl ? 'تقرير يومي' : 'Daily report'}
            </Text>
          </PressableScale>

          <PressableScale
            onPress={() => {
              setCreateSheetOpen(false);
              setToastMessage(rtl ? 'تم اختيار طلب مواد' : 'Creating Material request');
            }}
            className="flex-row items-center gap-3 rounded-control bg-surface p-3.5 border border-line"
          >
            <Icon name="cube" size={20} color={colors.action} />
            <Text variant="bodyStrong">
              {rtl ? 'طلب مواد' : 'Material request'}
            </Text>
          </PressableScale>

          <PressableScale
            onPress={() => {
              setCreateSheetOpen(false);
              setToastMessage(rtl ? 'تم اختيار ملاحظة أو مشكلة' : 'Creating Snag or issue');
            }}
            className="flex-row items-center gap-3 rounded-control bg-surface p-3.5 border border-line"
          >
            <Icon name="warning" size={20} color={colors.warning} />
            <Text variant="bodyStrong">
              {rtl ? 'ملاحظة أو مشكلة' : 'Snag or issue'}
            </Text>
          </PressableScale>

          <PressableScale
            onPress={() => {
              setCreateSheetOpen(false);
              setToastMessage(rtl ? 'تم اختيار تحديث بالصور' : 'Photo update');
            }}
            className="flex-row items-center gap-3 rounded-control bg-surface p-3.5 border border-line"
          >
            <Icon name="camera" size={20} color={colors.action} />
            <Text variant="bodyStrong">
              {rtl ? 'تحديث بالصور' : 'Photo update'}
            </Text>
          </PressableScale>

          <PressableScale
            onPress={() => {
              setCreateSheetOpen(false);
              setToastMessage(rtl ? 'تم اختيار استلام توريد' : 'Delivery received');
            }}
            className="flex-row items-center gap-3 rounded-control bg-surface p-3.5 border border-line"
          >
            <Icon name="cube" size={20} color={colors.action} />
            <Text variant="bodyStrong">
              {rtl ? 'استلام توريد' : 'Delivery received'}
            </Text>
          </PressableScale>
        </View>
      </BottomSheet>

      {/* Dialog matching Components forms, workflow, feedback.png */}
      <Dialog
        visible={dialogOpen}
        onClose={() => setDialogOpen(false)}
        title={rtl ? 'اعتماد 148,800 ج.م؟' : 'Approve EGP 148,800?'}
        description={
          rtl
            ? 'سيتم إصدار أمر شراء للمورد المختار. لا يمكن التراجع عن هذا الإجراء من التطبيق.'
            : 'A purchase order will be issued to the selected supplier. This cannot be undone from the app.'
        }
        confirmLabel={t('action.approve')}
        cancelLabel={t('action.cancel')}
        onConfirm={() => {
          setDialogOpen(false);
          setToastMessage(rtl ? 'تم الاعتماد بنجاح' : 'Approved EGP 148,800');
        }}
      />

      {/* Navigation Drawer matching NavigationDrawer board */}
      <NavigationDrawer
        visible={drawerOpen}
        onClose={() => setDrawerOpen(false)}
        userName={rtl ? 'عمر بهاء' : 'Omar Bahaa'}
        userRole={rtl ? 'مدير المشروع' : 'Project manager'}
        userAvatar="OB"
        items={drawerItems}
        activeKey={activeTab}
        onSelect={setActiveTab}
        currentLanguage={language as 'en' | 'ar'}
        onLanguageToggle={() => changeLanguage(language === 'en' ? 'ar' : 'en')}
        onSettingsPress={() => {
          setDrawerOpen(false);
          setToastMessage('Settings clicked');
        }}
      />
    </View>
  );
}